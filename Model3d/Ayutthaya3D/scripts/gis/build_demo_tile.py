"""Build a 500 m Ayutthaya demo tile from OpenStreetMap.

Source data is downloaded from the Overpass API and converted with PROJ
(EPSG:4326 -> EPSG:32647). Heights and road widths are tagged as measured
or estimated. Missing DEM is recorded as a flat placeholder, never invented.
"""

from __future__ import annotations

import json
import math
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

import requests
from pyproj import Transformer
from shapely.geometry import LineString, MultiLineString, MultiPolygon, Polygon, box
from shapely.ops import unary_union

ROOT = Path(__file__).resolve().parents[2]
RAW_DIR = ROOT / "data" / "raw"
PROCESSED_DIR = ROOT / "data" / "processed"
METADATA_DIR = ROOT / "data" / "metadata"
STREAMING_DIR = (
    ROOT
    / "ayutthaya simulator"
    / "My project"
    / "Assets"
    / "StreamingAssets"
    / "gis"
)

TILE_SIZE_M = 500.0
HALF = TILE_SIZE_M / 2.0
OVERPASS_URLS = (
    "https://overpass-api.de/api/interpreter",
    "https://overpass.kumi.systems/api/interpreter",
)
SEARCH_BBOX = (14.30, 100.50, 14.42, 100.62)  # south, west, north, east
FALLBACK_CENTER = (14.3568, 100.5615)
USER_AGENT = "AyutthayaSimulator-demo-tile/1.0 (local educational project; ODbL attribution)"

ROAD_WIDTH_M = {
    "motorway": 12.0,
    "trunk": 10.0,
    "primary": 9.0,
    "secondary": 7.5,
    "tertiary": 6.5,
    "unclassified": 5.0,
    "residential": 5.5,
    "living_street": 4.0,
    "service": 3.5,
    "pedestrian": 3.0,
    "footway": 1.5,
    "path": 1.2,
    "cycleway": 2.0,
    "track": 3.0,
    "steps": 1.5,
}

BUILDING_HEIGHT_M = {
    "house": 6.0,
    "residential": 6.0,
    "apartments": 12.0,
    "commercial": 8.0,
    "retail": 6.0,
    "industrial": 8.0,
    "warehouse": 7.0,
    "temple": 8.0,
    "ruins": 6.0,
    "religious": 8.0,
    "yes": 6.0,
}


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


def nominatim_center() -> dict | None:
    response = requests.get(
        "https://nominatim.openstreetmap.org/search",
        params={
            "q": "Wat Phra Si Sanphet, Phra Nakhon Si Ayutthaya, Thailand",
            "format": "jsonv2",
            "limit": 1,
        },
        headers={"User-Agent": USER_AGENT},
        timeout=40,
    )
    response.raise_for_status()
    payload = response.json()
    if not payload:
        return None
    item = payload[0]
    return {
        "selection": "nominatim_osm",
        "confidence": "geocoder_result",
        "name": item.get("name") or item.get("display_name"),
        "latitude": float(item["lat"]),
        "longitude": float(item["lon"]),
        "osmType": item.get("osm_type"),
        "osmId": item.get("osm_id"),
        "license": item.get("licence"),
        "sourceUrl": "https://nominatim.openstreetmap.org/search",
    }


def download_osm_map(south: float, west: float, north: float, east: float) -> dict:
    url = "https://api.openstreetmap.org/api/0.6/map"
    response = requests.get(
        url,
        params={"bbox": f"{west:.7f},{south:.7f},{east:.7f},{north:.7f}"},
        headers={"User-Agent": USER_AGENT},
        timeout=120,
    )
    response.raise_for_status()
    RAW_DIR.mkdir(parents=True, exist_ok=True)
    xml_path = RAW_DIR / "demo_tile_osm.xml"
    xml_path.write_bytes(response.content)
    root = ET.fromstring(response.content)
    nodes = {}
    named_nodes = []
    for node in root.findall("node"):
        tags = {tag.attrib["k"]: tag.attrib["v"] for tag in node.findall("tag")}
        record = {"lat": float(node.attrib["lat"]), "lon": float(node.attrib["lon"]), "tags": tags}
        nodes[node.attrib["id"]] = record
        if tags.get("name") or tags.get("name:en"):
            named_nodes.append({"id": node.attrib["id"], **record})
    elements = []
    for way in root.findall("way"):
        tags = {tag.attrib["k"]: tag.attrib["v"] for tag in way.findall("tag")}
        geometry = []
        missing = False
        for nd in way.findall("nd"):
            node = nodes.get(nd.attrib["ref"])
            if node is None:
                missing = True
                break
            geometry.append({"lat": node["lat"], "lon": node["lon"]})
        if missing or len(geometry) < 2:
            continue
        elements.append({"type": "way", "id": int(way.attrib["id"]), "tags": tags, "geometry": geometry})
    return {
        "elements": elements,
        "named_nodes": named_nodes,
        "_source_url": url,
        "rawXml": str(xml_path),
    }


def overpass(query: str) -> dict:
    last_error = None
    headers = {"User-Agent": USER_AGENT}
    for url in OVERPASS_URLS:
        try:
            response = requests.post(url, data={"data": query}, headers=headers, timeout=120)
            response.raise_for_status()
            payload = response.json()
            if "elements" not in payload:
                raise RuntimeError(f"Unexpected Overpass payload from {url}")
            payload["_source_url"] = url
            return payload
        except Exception as exc:  # noqa: BLE001 - try the next public endpoint
            last_error = exc
            print(f"Overpass endpoint failed: {url}: {exc}", file=sys.stderr)
    raise RuntimeError(f"All Overpass endpoints failed: {last_error}")


def find_center(transformer: Transformer) -> dict:
    south, west, north, east = SEARCH_BBOX
    query = f"""
[out:json][timeout:40];
(
  nwr["name:en"~"Wat Phra Si Sanphet",i]({south},{west},{north},{east});
  nwr["name"~"วัดพระศรีสรรเพชญ์"]({south},{west},{north},{east});
  nwr["name:en"~"Wat Mahathat",i]["historic"]({south},{west},{north},{east});
);
out center tags;
"""
    payload = overpass(query)
    candidates = []
    for element in payload.get("elements", []):
        lat = element.get("lat") or (element.get("center") or {}).get("lat")
        lon = element.get("lon") or (element.get("center") or {}).get("lon")
        if lat is None or lon is None:
            continue
        tags = element.get("tags") or {}
        name = tags.get("name:en") or tags.get("name") or ""
        candidates.append(
            {
                "osmType": element.get("type"),
                "osmId": element.get("id"),
                "name": name,
                "latitude": lat,
                "longitude": lon,
            }
        )
    preferred = None
    for item in candidates:
        lowered = item["name"].lower()
        if "sanphet" in lowered or "สรรเพชญ์" in item["name"]:
            preferred = item
            break
    if preferred is None and candidates:
        preferred = candidates[0]
    if preferred is None:
        lat, lon = FALLBACK_CENTER
        easting, northing = transformer.transform(lon, lat)
        return {
            "selection": "fallback_unverified_center",
            "confidence": "low",
            "name": "Ayutthaya historical island approximate center",
            "latitude": lat,
            "longitude": lon,
            "utmEasting": easting,
            "utmNorthing": northing,
            "note": "Named OSM feature was not returned. Tile center is an approximate fallback, not a surveyed monument coordinate.",
        }
    easting, northing = transformer.transform(preferred["longitude"], preferred["latitude"])
    preferred.update(
        {
            "selection": "osm_named_feature",
            "confidence": "osm_node_or_center",
            "utmEasting": easting,
            "utmNorthing": northing,
            "overpassUrl": payload.get("_source_url"),
        }
    )
    return preferred


def parse_float(value: str | None) -> float | None:
    if not value:
        return None
    token = value.lower().replace("m", "").replace(",", ".").strip()
    try:
        return float(token)
    except ValueError:
        return None


def road_width(tags: dict) -> tuple[float, str]:
    parsed = parse_float(tags.get("width"))
    if parsed is not None and 0.4 <= parsed <= 40:
        return parsed, "osm_width_tag"
    lanes = parse_float(tags.get("lanes"))
    if lanes is not None and 1 <= lanes <= 8:
        return lanes * 3.2, "estimated_from_lanes"
    highway = tags.get("highway", "unclassified")
    return ROAD_WIDTH_M.get(highway, 5.0), "estimated_default_for_highway_type"


def building_height(tags: dict) -> tuple[float, str, float | None]:
    height = parse_float(tags.get("height"))
    if height is not None and 1.5 <= height <= 120:
        return height, "osm_height_tag", parse_float(tags.get("building:levels"))
    levels = parse_float(tags.get("building:levels"))
    if levels is not None and 1 <= levels <= 40:
        return levels * 3.0, "estimated_from_building_levels_x3m", levels
    kind = tags.get("building", "yes")
    return BUILDING_HEIGHT_M.get(kind, 6.0), "estimated_default_for_building_type", levels


def coords_to_local(geometry: list[dict], transformer: Transformer, origin_e: float, origin_n: float) -> list[tuple[float, float]]:
    points = []
    for node in geometry:
        easting, northing = transformer.transform(node["lon"], node["lat"])
        points.append((easting - origin_e, northing - origin_n))
    return points


def clip_line(points: list[tuple[float, float]], tile) -> list[list[list[float]]]:
    if len(points) < 2:
        return []
    line = LineString(points)
    if not line.intersects(tile):
        return []
    clipped = line.intersection(tile)
    parts = []
    geoms = clipped.geoms if isinstance(clipped, MultiLineString) else [clipped]
    for geom in geoms:
        if geom.is_empty or geom.geom_type != "LineString" or len(geom.coords) < 2:
            continue
        parts.append([[round(x, 3), round(z, 3)] for x, z in geom.coords])
    return parts


def clip_polygon(points: list[tuple[float, float]], tile) -> list[list[list[float]]]:
    if len(points) < 4 and points[0] != points[-1]:
        points = points + [points[0]]
    if len(points) < 4:
        return []
    polygon = Polygon(points)
    if not polygon.is_valid:
        polygon = polygon.buffer(0)
    if polygon.is_empty or not polygon.intersects(tile):
        return []
    clipped = polygon.intersection(tile)
    geoms = clipped.geoms if isinstance(clipped, MultiPolygon) else [clipped]
    parts = []
    for geom in geoms:
        if geom.is_empty or geom.geom_type != "Polygon" or geom.area < 4:
            continue
        ring = [[round(x, 3), round(z, 3)] for x, z in geom.exterior.coords]
        if len(ring) >= 4:
            parts.append(ring)
    return parts


def pack_points(points: list[list[float]]) -> list[float]:
    packed: list[float] = []
    for point in points:
        packed.extend([point[0], point[1]])
    return packed


def unity_tile(tile_doc: dict) -> dict:
    origin = tile_doc["origin"]
    return {
        "schemaVersion": 1,
        "tileId": tile_doc["tileId"],
        "terrainStatus": tile_doc["status"]["terrain"],
        "originName": origin.get("name") or "",
        "originLatitude": origin["latitude"],
        "originLongitude": origin["longitude"],
        "originEasting": origin["utmEasting"],
        "originNorthing": origin["utmNorthing"],
        "attribution": tile_doc["license"]["attribution"],
        "licenseName": tile_doc["license"]["license"],
        "buildings": [
            {
                "id": item["id"],
                "name": item["name"],
                "buildingType": item["buildingType"],
                "historic": item["historic"],
                "heightMeters": item["heightMeters"],
                "heightSource": item["heightSource"],
                "confidence": item["confidence"],
                "footprint": pack_points(item["footprint"]),
            }
            for item in tile_doc["buildings"]
        ],
        "roads": [
            {
                "id": item["id"],
                "name": item["name"],
                "highway": item["highway"],
                "widthMeters": item["widthMeters"],
                "widthSource": item["widthSource"],
                "bridge": 1 if item["bridge"] else 0,
                "points": pack_points(item["points"]),
            }
            for item in tile_doc["roads"]
        ],
        "water": [
            {
                "id": item["id"],
                "name": item["name"],
                "kind": item["kind"],
                "shape": item["shape"],
                "widthMeters": item["widthMeters"],
                "widthSource": item["widthSource"],
                "points": pack_points(item["points"]),
            }
            for item in tile_doc["water"]
        ],
    }


def main() -> None:
    RAW_DIR.mkdir(parents=True, exist_ok=True)
    PROCESSED_DIR.mkdir(parents=True, exist_ok=True)
    METADATA_DIR.mkdir(parents=True, exist_ok=True)
    STREAMING_DIR.mkdir(parents=True, exist_ok=True)

    transformer = Transformer.from_crs("EPSG:4326", "EPSG:32647", always_xy=True)
    downloaded_at = utc_now()
    try:
        center = nominatim_center()
    except Exception as exc:  # noqa: BLE001
        print(f"Nominatim failed, using documented fallback center: {exc}", file=sys.stderr)
        center = None
    if center is None:
        lat, lon = FALLBACK_CENTER
        center = {
            "selection": "fallback_unverified_center",
            "confidence": "low",
            "name": "Ayutthaya historical island approximate center",
            "latitude": lat,
            "longitude": lon,
            "note": "Geocoder did not return a feature. This center is approximate and is not a surveyed monument coordinate.",
        }
    lat = center["latitude"]
    lon = center["longitude"]
    easting, northing = transformer.transform(lon, lat)
    center["utmEasting"] = easting
    center["utmNorthing"] = northing
    dlat = (HALF + 30) / 110574.0
    dlon = (HALF + 30) / (111320.0 * math.cos(math.radians(lat)))
    south, north = lat - dlat, lat + dlat
    west, east = lon - dlon, lon + dlon

    payload = download_osm_map(south, west, north, east)
    raw_path = RAW_DIR / "demo_tile_osm_elements.json"
    raw_path.write_text(
        json.dumps({"source": payload.get("_source_url"), "elements": payload["elements"]}, ensure_ascii=False),
        encoding="utf-8",
    )

    origin_e = center["utmEasting"]
    origin_n = center["utmNorthing"]
    tile = box(-HALF, -HALF, HALF, HALF)
    buildings = []
    roads = []
    water = []
    skipped = {"invalid_building": 0, "invalid_road": 0, "invalid_water": 0}

    for element in payload.get("elements", []):
        tags = element.get("tags") or {}
        geometry = element.get("geometry") or []
        if element.get("type") != "way" or len(geometry) < 2:
            continue
        local = coords_to_local(geometry, transformer, origin_e, origin_n)
        osm_id = f"way/{element.get('id')}"
        if "building" in tags:
            parts = clip_polygon(local, tile)
            if not parts:
                skipped["invalid_building"] += 1
                continue
            height, height_source, levels = building_height(tags)
            for index, ring in enumerate(parts):
                buildings.append(
                    {
                        "id": osm_id if index == 0 else f"{osm_id}#{index}",
                        "name": tags.get("name:en") or tags.get("name") or "",
                        "buildingType": tags.get("building", "yes"),
                        "historic": tags.get("historic", ""),
                        "footprint": ring,
                        "heightMeters": round(height, 2),
                        "heightSource": height_source,
                        "levels": levels,
                        "confidence": "footprint_from_osm"
                        if height_source == "osm_height_tag"
                        else "footprint_from_osm_height_estimated",
                    }
                )
        elif "highway" in tags:
            parts = clip_line(local, tile)
            if not parts:
                skipped["invalid_road"] += 1
                continue
            width, width_source = road_width(tags)
            for index, line in enumerate(parts):
                roads.append(
                    {
                        "id": osm_id if index == 0 else f"{osm_id}#{index}",
                        "name": tags.get("name:en") or tags.get("name") or "",
                        "highway": tags.get("highway"),
                        "surface": tags.get("surface", ""),
                        "widthMeters": round(width, 2),
                        "widthSource": width_source,
                        "points": line,
                        "bridge": tags.get("bridge", "") == "yes",
                        "layer": tags.get("layer", "0"),
                    }
                )
        elif "waterway" in tags or tags.get("natural") == "water":
            kind = tags.get("waterway") or tags.get("natural")
            if kind in {"riverbank", "river", "canal", "stream", "ditch", "water"}:
                if local[0] == local[-1] and len(local) >= 4:
                    parts = clip_polygon(local, tile)
                    shape_type = "polygon"
                else:
                    parts = clip_line(local, tile)
                    shape_type = "line"
                if not parts:
                    skipped["invalid_water"] += 1
                    continue
                width, width_source = road_width({"width": tags.get("width"), "highway": "unclassified"})
                if kind in {"river", "canal"} and width_source != "osm_width_tag":
                    width, width_source = 18.0, "estimated_default_for_waterway"
                for index, part in enumerate(parts):
                    water.append(
                        {
                            "id": osm_id if index == 0 else f"{osm_id}#{index}",
                            "name": tags.get("name:en") or tags.get("name") or "",
                            "kind": kind,
                            "shape": shape_type,
                            "widthMeters": round(width, 2),
                            "widthSource": width_source,
                            "points": part,
                        }
                    )

    tile_doc = {
        "schemaVersion": 1,
        "tileId": "ayutthaya_demo_500m",
        "generatedAt": downloaded_at,
        "status": {
            "terrain": "PLACEHOLDER_FLAT_NO_DEM",
            "terrainNote": "No DEM raster was downloaded. Ground is a flat 500 m plane at 0 m, not surveyed elevation.",
            "roads": "OSM_GEOMETRY",
            "buildings": "OSM_FOOTPRINTS",
            "water": "OSM_GEOMETRY",
            "historicalModels": "NOT_CREATED",
            "historicalNote": "Named historic tags are metadata only. This tile does not claim reconstructed temple architecture.",
        },
        "origin": {
            "crsSource": "EPSG:4326",
            "crsProjected": "EPSG:32647",
            "utmZone": 47,
            "latitude": center["latitude"],
            "longitude": center["longitude"],
            "utmEasting": center["utmEasting"],
            "utmNorthing": center["utmNorthing"],
            "selection": center.get("selection"),
            "name": center.get("name"),
            "osmId": center.get("osmId"),
            "confidence": center.get("confidence"),
        },
        "tile": {
            "sizeMeters": TILE_SIZE_M,
            "minX": -HALF,
            "maxX": HALF,
            "minZ": -HALF,
            "maxZ": HALF,
            "queryBboxWgs84": {"south": south, "west": west, "north": north, "east": east},
        },
        "license": {
            "source": "OpenStreetMap",
            "sourceUrl": "https://www.openstreetmap.org",
            "api": payload.get("_source_url"),
            "license": "Open Database License (ODbL) 1.0",
            "licenseUrl": "https://opendatacommons.org/licenses/odbl/1-0/",
            "attribution": "© OpenStreetMap contributors",
            "downloadedAt": downloaded_at,
            "copyrightNote": "Contains information from OpenStreetMap, which is made available under the Open Database License (ODbL).",
        },
        "counts": {
            "buildings": len(buildings),
            "roads": len(roads),
            "water": len(water),
            "skipped": skipped,
            "rawElements": len(payload.get("elements", [])),
        },
        "buildings": buildings,
        "roads": roads,
        "water": water,
    }

    processed_path = PROCESSED_DIR / "demo_tile_500m.json"
    text = json.dumps(tile_doc, ensure_ascii=False, indent=2)
    processed_path.write_text(text, encoding="utf-8")
    (STREAMING_DIR / "demo_tile_500m.json").write_text(text, encoding="utf-8")
    unity_text = json.dumps(unity_tile(tile_doc), ensure_ascii=False)
    unity_path = PROCESSED_DIR / "demo_tile_500m.unity.json"
    unity_path.write_text(unity_text, encoding="utf-8")
    (STREAMING_DIR / "demo_tile_500m.unity.json").write_text(unity_text, encoding="utf-8")

    metadata = {
        "downloadedAt": downloaded_at,
        "rawFile": str(raw_path),
        "processedFile": str(processed_path),
        "license": tile_doc["license"],
        "origin": tile_doc["origin"],
        "counts": tile_doc["counts"],
        "dem": {
            "status": "not_downloaded",
            "reason": "No Copernicus/OpenTopography API key was available in this environment. Elevation was not invented.",
        },
    }
    (METADATA_DIR / "demo_tile_500m.metadata.json").write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps({"origin": tile_doc["origin"], "counts": tile_doc["counts"], "bbox": tile_doc["tile"]["queryBboxWgs84"]}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
