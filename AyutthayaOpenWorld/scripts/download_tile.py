#!/usr/bin/env python3
"""Download and prepare one source-traceable 1 km Ayutthaya GIS tile."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from datetime import datetime, timezone
from pathlib import Path
import time
import urllib.parse
import urllib.request

import numpy as np
import tifffile
from pyproj import Transformer
from shapely.geometry import LineString, Polygon, mapping, shape, box


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def fetch(url: str, destination: Path, *, timeout=180) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    request = urllib.request.Request(url, headers={"User-Agent": "AyutthayaOpenWorldGIS/0.1 (tile data downloader)"})
    with urllib.request.urlopen(request, timeout=timeout) as response, destination.open("wb") as output:
        while block := response.read(1024 * 1024):
            output.write(block)
    if not destination.exists() or destination.stat().st_size == 0:
        raise RuntimeError(f"Empty download: {url}")


def overpass_query(bbox_wgs84):
    south, west, north, east = bbox_wgs84
    return (
        f"[out:json][timeout:90];(way[highway]({south},{west},{north},{east});"
        f"way[waterway]({south},{west},{north},{east});"
        f"way[building]({south},{west},{north},{east}););out body;>;out skel qt;"
    )


def osm_to_geojson(raw_path: Path, out_path: Path, to_projected, origin_xy, clip_box):
    doc = json.loads(raw_path.read_text(encoding="utf-8"))
    nodes, ways = {}, []
    for element in doc.get("elements", []):
        if element.get("type") == "node":
            nodes[int(element["id"])] = (float(element["lon"]), float(element["lat"]))
        elif element.get("type") == "way":
            ways.append(element)
    features = []
    for way in ways:
        coords = [nodes[node_id] for node_id in way.get("nodes", []) if node_id in nodes]
        if len(coords) < 2:
            continue
        xs, ys = to_projected.transform([point[0] for point in coords], [point[1] for point in coords])
        local = [(float(x) - origin_xy[0], float(y) - origin_xy[1]) for x, y in zip(xs, ys)]
        tags = way.get("tags", {})
        is_area = len(local) >= 4 and way.get("nodes", [None])[0] == way.get("nodes", [None])[-1]
        geom = Polygon(local) if is_area else LineString(local)
        clipped = geom.intersection(clip_box)
        if clipped.is_empty:
            continue
        # preserve true line/polygon geometry after clipping; split results remain explicit parts
        parts = list(clipped.geoms) if clipped.geom_type.startswith("Multi") or clipped.geom_type == "GeometryCollection" else [clipped]
        for part_index, part in enumerate(parts):
            if part.geom_type not in {"LineString", "Polygon"} or part.is_empty:
                continue
            features.append({
                "type": "Feature",
                "id": f"way/{way['id']}/{part_index}",
                "properties": {"osm_id": way["id"], "source": "OpenStreetMap", "tags": tags},
                "geometry": mapping(part),
            })
    out_path.write_text(json.dumps({"type": "FeatureCollection", "name": "OSM local UTM47N metres", "features": features}, ensure_ascii=False, indent=2), encoding="utf-8")
    return len(features)


def run(config_path: Path, *, tile_id=None, output_root=None, sleep_seconds=1.0):
    cfg = json.loads(config_path.read_text(encoding="utf-8"))
    project, sources = cfg["project"], cfg["sources"]
    tile_id = tile_id or project["tile_id"]
    root = Path(output_root or config_path.parent.parent / "data") / tile_id
    root.mkdir(parents=True, exist_ok=True)

    lon0, lat0 = float(project["local_origin"]["longitude"]), float(project["local_origin"]["latitude"])
    crs = project["projected_crs"]
    to_utm = Transformer.from_crs("EPSG:4326", crs, always_xy=True)
    to_wgs = Transformer.from_crs(crs, "EPSG:4326", always_xy=True)
    origin_x, origin_y = to_utm.transform(lon0, lat0)
    size = float(project["tile_size_m"])
    half = size / 2
    xmin, ymin, xmax, ymax = origin_x-half, origin_y-half, origin_x+half, origin_y+half
    perimeter_x = [xmin, origin_x, xmax, xmax, xmax, origin_x, xmin, xmin, xmin]
    perimeter_y = [ymin, ymin, ymin, origin_y, ymax, ymax, ymax, origin_y, ymin]
    lons, lats = to_wgs.transform(perimeter_x, perimeter_y)
    bbox = [min(lats), min(lons), max(lats), max(lons)]

    dem = sources["dem"]
    degree_lat, degree_lon = int(dem["tile_latitude_degree"]), int(dem["tile_longitude_degree"])
    dem_name = f"Copernicus_DSM_COG_10_N{degree_lat:02d}_00_E{degree_lon:03d}_00_DEM"
    dem_url = dem["url_template"].format(lat=degree_lat, lon=degree_lon)
    raw_dem = root / f"{dem_name}.tif"
    if not raw_dem.exists():
        print(f"Downloading authentic Copernicus source: {dem_url}", flush=True)
        fetch(dem_url, raw_dem)

    # The inspected GLO-30 COG is WGS84 (EPSG:4326), pixel-is-point, 1 arc-second,
    # 3600x3600 float32. Validate tags rather than guessing a georeference.
    with tifffile.TiffFile(raw_dem) as tiff:
        page = tiff.pages[0]
        if page.shape != (3600, 3600) or page.dtype != np.dtype("float32"):
            raise RuntimeError(f"Unexpected Copernicus GLO-30 layout: {page.shape}, {page.dtype}")
        scale_tag, tie_tag, keys_tag = (page.tags.get(name) for name in ("ModelPixelScaleTag", "ModelTiepointTag", "GeoKeyDirectoryTag"))
        if not scale_tag or not tie_tag or not keys_tag:
            raise RuntimeError("DEM lacks required georeference tags; refusing to infer coordinates")
        scale_lon, scale_lat = map(float, scale_tag.value[:2])
        i0, j0, _, lon_tie, lat_tie, _ = map(float, tie_tag.value[:6])
        if 4326 not in tuple(keys_tag.value) or abs(scale_lon-1/3600)>1e-10 or abs(scale_lat-1/3600)>1e-10:
            raise RuntimeError("DEM CRS/pixel spacing differs from documented Copernicus GLO-30")
        # One 1-degree source COG is decoded per 1-km processing job (about 52 MiB).
        source_dem = page.asarray(maxworkers=2)

    resolution = float(project["tile_resolution_m"])
    subdivisions = int(math.ceil(size / resolution))
    width = height = subdivisions + 1
    cell_x, cell_y = size/(width-1), size/(height-1)
    # Vertex samples include the exact projected tile boundaries, so mesh bounds are exactly 1 km.
    target_x = np.linspace(xmin, xmax, width, dtype=np.float64)
    target_y = np.linspace(ymax, ymin, height, dtype=np.float64)
    xx, yy = np.meshgrid(target_x, target_y)
    target_lon, target_lat = to_wgs.transform(xx, yy)
    source_col = (target_lon-lon_tie)/scale_lon+i0
    source_row = (lat_tie-target_lat)/scale_lat+j0
    c0, r0 = np.floor(source_col).astype(np.int32), np.floor(source_row).astype(np.int32)
    c1, r1 = c0+1, r0+1
    inside = (c0>=0)&(r0>=0)&(c1<source_dem.shape[1])&(r1<source_dem.shape[0])
    arr = np.full((height,width),np.nan,dtype=np.float32)
    rr,cc=np.nonzero(inside)
    if len(rr):
        dr=source_row[rr,cc]-r0[rr,cc]; dc=source_col[rr,cc]-c0[rr,cc]
        a=source_dem[r0[rr,cc],c0[rr,cc]]; b=source_dem[r0[rr,cc],c1[rr,cc]]
        c=source_dem[r1[rr,cc],c0[rr,cc]]; d=source_dem[r1[rr,cc],c1[rr,cc]]
        arr[rr,cc]=(a*(1-dc)*(1-dr)+b*dc*(1-dr)+c*(1-dc)*dr+d*dc*dr).astype(np.float32)
    valid=np.isfinite(arr)&(arr>-1000)
    if int(valid.sum()) < arr.size*0.95:
        raise RuntimeError(f"DEM coverage insufficient: {int(valid.sum())}/{arr.size}; no synthetic fill permitted")
    arr[~valid]=np.nan
    origin_col=(lon0-lon_tie)/scale_lon+i0
    origin_row=(lat_tie-lat0)/scale_lat+j0
    oc0,or0=int(math.floor(origin_col)),int(math.floor(origin_row))
    if oc0<0 or or0<0 or oc0+1>=source_dem.shape[1] or or0+1>=source_dem.shape[0]:
        raise RuntimeError("Configured local origin falls outside source DEM tile")
    odc,odr=origin_col-oc0,origin_row-or0
    oa=source_dem[or0,oc0]; ob=source_dem[or0,oc0+1]
    oc=source_dem[or0+1,oc0]; od=source_dem[or0+1,oc0+1]
    origin_elevation=float(oa*(1-odc)*(1-odr)+ob*odc*(1-odr)+oc*(1-odc)*odr+od*odc*odr)
    terrain=root/"terrain_30m.npy"
    np.save(terrain,arr)
    valid_path=root/"terrain_valid.npy"
    np.save(valid_path,valid)

    time.sleep(max(0,sleep_seconds))
    query=overpass_query(bbox)
    osm_url=sources["osm"]["overpass_url"]
    raw_osm=root/"osm_overpass.json"
    # Reuse a prior real response unless the caller explicitly asks to refresh it.
    if not raw_osm.exists() or raw_osm.stat().st_size == 0:
        payload=urllib.parse.urlencode({"data":query}).encode("utf-8")
        request=urllib.request.Request(osm_url,data=payload,headers={"User-Agent":"AyutthayaOpenWorldGIS/0.1","Content-Type":"application/x-www-form-urlencoded"},method="POST")
        with urllib.request.urlopen(request,timeout=180) as response:
            raw_osm.write_bytes(response.read())
    else:
        cached=json.loads(raw_osm.read_text(encoding="utf-8"))
        if "elements" not in cached:
            raise RuntimeError("Cached OSM response is invalid; refusing to treat an error document as map data")
    osm_local=root/"osm.geojson"
    clip=box(-half,-half,half,half)
    feature_count=osm_to_geojson(raw_osm,osm_local,to_utm,(origin_x,origin_y),clip)

    manifest={
        "tile_id":tile_id,"retrieved_utc":datetime.now(timezone.utc).isoformat(),"crs":crs,"units":"metres",
        "extent_local_m":[-half,-half,half,half],"extent_projected_m":[xmin,ymin,xmax,ymax],
        "local_origin":{"longitude":lon0,"latitude":lat0,"projected_easting_m":origin_x,"projected_northing_m":origin_y,**project["local_origin"]},
        "dem":{"url":dem_url,"provider":dem["provider"],"product_type":dem["product_type"],"license":dem["license"],"attribution":dem["attribution"],"metadata_url":dem["metadata_url"],"sha256_source_geotiff":sha256(raw_dem),"sha256_processed_array":sha256(terrain),"sha256_valid_mask":sha256(valid_path),"origin_elevation_m":origin_elevation,"valid_cells":int(valid.sum()),"total_cells":int(arr.size),"resolution_m":resolution,"grid_vertex_spacing_xy_m":[cell_x,cell_y],"grid_layout":"vertices_include_exact_tile_bounds"},
        "osm":{"url":osm_url,"query_bbox_s_w_n_e":bbox,"query":query,"license":sources["osm"]["license"],"attribution":sources["osm"]["attribution"],"copyright_url":sources["osm"]["copyright_url"],"sha256_raw_response":sha256(raw_osm),"sha256_local_geojson":sha256(osm_local),"feature_count":feature_count},
        "files":{"terrain_array":terrain.name,"terrain_valid_mask":valid_path.name,"osm_geojson":osm_local.name,"raw_osm":raw_osm.name,"source_dem":raw_dem.name},
    }
    manifest_path=root/"sources.json"
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps({"tile":tile_id,"terrain":str(terrain),"shape":list(arr.shape),"valid_cells":int(valid.sum()),"osm_features":feature_count,"manifest":str(manifest_path)},indent=2))
    return manifest


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config",type=Path,default=Path(__file__).parents[1]/"config/project.json")
    parser.add_argument("--tile-id")
    parser.add_argument("--output-root",type=Path)
    parser.add_argument("--sleep-seconds",type=float,default=1.0)
    args=parser.parse_args()
    run(args.config,tile_id=args.tile_id,output_root=args.output_root,sleep_seconds=args.sleep_seconds)

if __name__=="__main__":
    main()
