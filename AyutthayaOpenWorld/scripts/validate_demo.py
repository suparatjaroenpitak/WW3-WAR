#!/usr/bin/env python3
"""Validate downloaded 1 km source data and Blender engine-export artifacts."""
import argparse
import json
from pathlib import Path
import struct
import sys

import numpy as np


def validate(project_root: Path, tile_id: str):
    data=project_root/"data"/tile_id
    output=project_root/"output"/tile_id
    config=json.loads((project_root/"config/project.json").read_text(encoding="utf-8"))
    manifest=json.loads((data/"sources.json").read_text(encoding="utf-8"))
    arr=np.load(data/"terrain_30m.npy",mmap_mode="r")
    mask=np.load(data/"terrain_valid.npy",mmap_mode="r")
    assert arr.shape==(34,34),f"Expected 34x34 tile at 30m; got {arr.shape}"
    assert mask.shape==arr.shape and bool(mask.all()),"Terrain has missing samples"
    assert np.isfinite(arr).all(),"Terrain array contains no-data"
    assert manifest["dem"]["valid_cells"]==arr.size,"Manifest does not account for every DEM cell"
    assert manifest["dem"]["product_type"]=="DSM","Unexpected DEM product type"
    assert manifest["osm"]["license"]=="ODbL 1.0","OSM license metadata missing"
    features=json.loads((data/"osm.geojson").read_text(encoding="utf-8"))["features"]
    assert len(features)==manifest["osm"]["feature_count"],"OSM feature count mismatch"
    for feature in features:
        for x,y in iter_xy(feature["geometry"]):
            assert -500.001<=x<=500.001 and -500.001<=y<=500.001, f"Feature outside tile: {feature.get('id')}"
    report=json.loads((output/"build_report.json").read_text(encoding="utf-8"))
    assert report["terrain_vertices"]==35*35 and report["terrain_faces"]==34*34
    assert report["collision_exported"] is True
    assert report["osm_road_objects"]>0 and report["osm_waterway_objects"]>0 and report["osm_building_footprints"]>0
    for name in ("demo.blend","demo.glb","demo.fbx","collision_terrain.fbx","sources.json"):
        path=output/name
        assert path.is_file() and path.stat().st_size>0, f"Missing/empty artifact: {path}"
    glb=(output/"demo.glb").read_bytes()
    assert glb[:4]==b"glTF" and struct.unpack("<II",glb[4:12])[0]==2
    json_length=struct.unpack("<I",glb[12:16])[0]
    doc=json.loads(glb[20:20+json_length].decode("utf-8"))
    assert doc.get("meshes") and doc.get("materials"),"GLB is missing mesh/material payloads"
    report_out={"status":"PASS","tile_id":tile_id,"terrain_shape":list(arr.shape),"osm_features":len(features),"artifacts":{name:(output/name).stat().st_size for name in ("demo.blend","demo.glb","demo.fbx","collision_terrain.fbx","sources.json")},"glb_meshes":len(doc["meshes"]),"glb_materials":len(doc["materials"])}
    print(json.dumps(report_out,ensure_ascii=False,indent=2))
    return report_out


def iter_xy(geometry):
    def walk(coords):
        if isinstance(coords,(list,tuple)) and len(coords)>=2 and isinstance(coords[0],(int,float)) and isinstance(coords[1],(int,float)):
            yield float(coords[0]),float(coords[1])
        elif isinstance(coords,(list,tuple)):
            for value in coords:
                yield from walk(value)
    yield from walk(geometry.get("coordinates",[]))


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--project-root",type=Path,default=Path(__file__).parents[1])
    parser.add_argument("--tile-id",default="AYU_UTM47N_E000_N000")
    args=parser.parse_args()
    validate(args.project_root.resolve(),args.tile_id)

if __name__=="__main__":
    main()
