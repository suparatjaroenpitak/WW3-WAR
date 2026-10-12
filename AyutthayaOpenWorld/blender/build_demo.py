"""Build one GIS demo tile in Blender. Run with Blender Python or Blender MCP execute code."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import sys

import bpy
import numpy as np
from mathutils import Vector


def material(name, color, roughness=0.9):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
    return mat


def collection(name, parent):
    coll = bpy.data.collections.new(name)
    parent.children.link(coll)
    return coll


def link_object(obj, coll):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)
    return obj


def mesh_object(name, verts, faces, coll, mat, source, uv_values=None):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    if mat:
        mesh.materials.append(mat)
    if uv_values:
        uv = mesh.uv_layers.new(name="UVMap")
        for poly in mesh.polygons:
            for loop_index in poly.loop_indices:
                vi = mesh.loops[loop_index].vertex_index
                uv.data[loop_index].uv = uv_values[vi]
    obj["source"] = source
    obj["units"] = "metres"
    return obj


def main(config_path: Path, data_root: Path, output_root: Path):
    cfg = json.loads(config_path.read_text(encoding="utf-8"))
    p, sources = cfg["project"], cfg["sources"]
    tile_id = p["tile_id"]
    tile = data_root / tile_id
    manifest = json.loads((tile / "sources.json").read_text(encoding="utf-8"))
    grid = np.load(tile / "terrain_30m.npy", mmap_mode="r")
    if grid.ndim != 2 or min(grid.shape) < 3 or not np.isfinite(grid).all():
        raise RuntimeError("Terrain raster is missing, invalid, or contains no-data; refusing to build a fabricated surface")
    valid_mask = np.load(tile / "terrain_valid.npy", mmap_mode="r")
    if valid_mask.shape != grid.shape or not bool(valid_mask.all()):
        raise RuntimeError("Terrain valid mask is incomplete or mismatched; refusing to create geometry")
    h, w = grid.shape
    size = float(p["tile_size_m"])
    origin_z = float(manifest["dem"]["origin_elevation_m"])
    # Raster cells are source samples. Construct a grid over exact tile bounds and bilinearly
    # resample only the real DEM values (no extrapolation beyond source edge except edge clamp).
    rows, cols = np.mgrid[0:h+1, 0:w+1]
    src_r = np.clip(rows * (h-1) / h, 0, h-1)
    src_c = np.clip(cols * (w-1) / w, 0, w-1)
    r0, c0 = np.floor(src_r).astype(int), np.floor(src_c).astype(int)
    r1, c1 = np.minimum(r0+1, h-1), np.minimum(c0+1, w-1)
    fr, fc = src_r-r0, src_c-c0
    z = (grid[r0,c0]*(1-fr)*(1-fc) + grid[r1,c0]*fr*(1-fc) + grid[r0,c1]*(1-fr)*fc + grid[r1,c1]*fr*fc) - origin_z
    verts, faces, uv_values = [], [], []
    for row in range(h+1):
        y = size/2 - row*size/h
        for col in range(w+1):
            x = -size/2 + col*size/w
            verts.append((float(x), float(y), float(z[row,col])))
            uv_values.append((col/w, 1-row/h))
    for row in range(h):
        for col in range(w):
            a=row*(w+1)+col
            faces.append((a,a+1,a+w+2,a+w+1))

    # Keep the current user's scene but isolate this deliverable in a clearly named root.
    root = collection("AYU_" + tile_id, bpy.context.scene.collection)
    terrain_coll = collection("Terrain", root)
    roads_coll = collection("Roads_OSM", root)
    water_coll = collection("Waterways_OSM", root)
    buildings_coll = collection("BuildingFootprints_OSM", root)
    collision_coll = collection("Collision", root)
    terrain_mat = material("M_CopernicusDEM_Neutral", (0.20,0.29,0.16))
    road_mat = material("M_OSM_Road_InferredWidth", (0.16,0.16,0.15))
    water_mat = material("M_OSM_Waterway", (0.10,0.28,0.42))
    building_mat = material("M_OSM_BuildingFootprint", (0.48,0.36,0.25))
    terrain_obj = mesh_object("Terrain_"+tile_id, verts, faces, terrain_coll, terrain_mat, sources["dem"]["provider"], uv_values)
    terrain_obj["elevation_datum"] = "Relative to DEM sample at configured local origin; original DEM values in manifest"
    terrain_obj["tile_extent_m"] = size
    terrain_obj["dem_resolution_m"] = manifest["dem"]["resolution_m"]
    terrain_obj["tile_lod"] = 0
    terrain_obj["collision_role"] = "terrain_collision"
    # Preserve independent, explicit collision mesh; no Unity/Unreal-specific invisible scale.
    coll_obj = mesh_object("Collision_Terrain_"+tile_id, verts, faces, collision_coll, None, "Derived from " + sources["dem"]["provider"], uv_values)
    coll_obj.display_type = "WIRE"
    coll_obj.hide_render = True
    coll_obj["collision_only"] = True
    coll_obj["lod_role"] = "collision"

    # Clip all OSM geometry to the same exact local tile bounds before meshing.
    feature_doc=json.loads((tile/"osm.geojson").read_text(encoding="utf-8"))
    features=feature_doc.get("features", [])
    road_count=water_count=building_count=0
    cell_x=float(manifest["dem"]["grid_cell_size_xy_m"][0])
    cell_y=float(manifest["dem"]["grid_cell_size_xy_m"][1])
    def terrain_height(x,y):
        col=max(0,min(w-1,int((x+size/2)/cell_x)))
        row=max(0,min(h-1,int((size/2-y)/cell_y)))
        return float(grid[row,col])-origin_z
    def add_curve(name, coords, coll, mat, width, tags):
        if len(coords)<2: return None
        curve=bpy.data.curves.new(name+"Curve","CURVE")
        curve.dimensions="3D"
        curve.resolution_u=1
        curve.bevel_depth=width/2
        curve.bevel_resolution=1
        spl=curve.splines.new("POLY")
        spl.points.add(len(coords)-1)
        for pt,(x,y) in zip(spl.points,coords):
            pt.co=(x,y,terrain_height(x,y)+0.20,1)
        obj=bpy.data.objects.new(name,curve)
        coll.objects.link(obj)
        curve.materials.append(mat)
        obj["source"]="OpenStreetMap way"
        obj["osm_tags"]=json.dumps(tags,ensure_ascii=False)
        obj["width_status"]="inferred from OSM class; not a surveyed width"
        obj["estimated_width_m"]=width
        return obj
    for feat in features:
        props=feat.get("properties",{})
        tags=props.get("tags",{})
        geom=feat.get("geometry") or {}
        gtype=geom.get("type")
        coords=geom.get("coordinates",[])
        if tags.get("highway") and gtype=="LineString":
            # Cartographic placeholder widths are inferred, never represented as OSM measurements.
            highway=tags.get("highway")
            width=6.0 if highway in {"motorway","trunk","primary"} else 4.0 if highway in {"secondary","tertiary"} else 2.5
            obj=add_curve("OSM_Road_"+str(props.get("osm_id")),coords,roads_coll,road_mat,width,tags)
            if obj:
                obj["bridge_tag"]=tags.get("bridge","no")
                road_count+=1
        elif tags.get("waterway") and gtype=="LineString":
            obj=add_curve("OSM_Waterway_"+str(props.get("osm_id")),coords,water_coll,water_mat,2.0,tags)
            if obj: obj["width_status"]="visualized at inferred 2 m; not surveyed"; water_count+=1
        elif tags.get("building") and gtype=="Polygon":
            ring=coords[0] if coords else []
            if len(ring)<4: continue
            ring=[(float(x),float(y)) for x,y in ring if -size/2 <= float(x) <= size/2 and -size/2 <= float(y) <= size/2]
            bverts=[(x,y,terrain_height(x,y)+0.25) for x,y in ring]
            if len(bverts)<3: continue
            obj=mesh_object("OSM_BuildingFootprint_"+str(props.get("osm_id")),bverts,[tuple(range(len(bverts)))],buildings_coll,building_mat,"OpenStreetMap building footprint",[(i/max(1,len(bverts)-1),0.0) for i in range(len(bverts))])
            obj["osm_tags"]=json.dumps(tags,ensure_ascii=False)
            obj["height_status"]="footprint only; no unsourced building height or facade generated"
            building_count+=1
    # Preserve provenance as scene custom properties.
    scene=bpy.context.scene
    scene["project"]="Ayutthaya Open World GIS demo"
    scene["tile_id"]=tile_id
    scene["crs"] = p["projected_crs"]
    scene["local_origin_lonlat"]=[p["local_origin"]["longitude"],p["local_origin"]["latitude"]]
    scene["tile_sources_manifest"]=str((tile/"sources.json").resolve())
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1.0

    out=output_root/tile_id
    out.mkdir(parents=True,exist_ok=True)
    blend=out/"demo.blend"
    glb=out/"demo.glb"
    fbx=out/"demo.fbx"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    export_objects=[o for c in (terrain_coll,roads_coll,water_coll,buildings_coll) for o in c.objects]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in export_objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=terrain_obj
    glb_result=bpy.ops.export_scene.gltf(filepath=str(glb),export_format="GLB",use_selection=True,export_apply=True)
    if "FINISHED" not in glb_result or not glb.exists() or glb.stat().st_size == 0:
        raise RuntimeError(f"GLB export failed: {glb_result}")
    # Blender FBX's exporter does not expose CURVE as an object type. Convert the selected
    # presentation curves to meshes after saving the native .blend and GLB; only this export
    # representation is changed.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in export_objects:
        if obj.type == "CURVE": obj.select_set(True)
    if bpy.context.selected_objects:
        bpy.context.view_layer.objects.active=bpy.context.selected_objects[0]
        bpy.ops.object.convert(target="MESH")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in export_objects:
        if obj.name in bpy.data.objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=terrain_obj
    fbx_result=bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,apply_unit_scale=True,apply_scale_options="FBX_SCALE_UNITS",bake_space_transform=False,object_types={"MESH"})
    if "FINISHED" not in fbx_result or not fbx.exists() or fbx.stat().st_size == 0:
        raise RuntimeError(f"FBX export failed: {fbx_result}")
    summary={"tile":tile_id,"blend":str(blend),"glb":str(glb),"fbx":str(fbx),"artifact_bytes":{"blend":blend.stat().st_size,"glb":glb.stat().st_size,"fbx":fbx.stat().st_size},"terrain_vertices":len(verts),"terrain_faces":len(faces),"osm_road_objects":road_count,"osm_waterway_objects":water_count,"osm_building_footprints":building_count,"collision_mesh":"Collision_Terrain_"+tile_id,"collision_exported":False,"notes":"Road/river display widths are estimated; building footprints are not extruded. Collision is separate in Blender but is not included in GLB/FBX exports; see source manifest."}
    report=out/"build_report.json"
    report.write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding="utf-8")
    print("AYU_BUILD_REPORT="+json.dumps(summary,ensure_ascii=False))
    return summary


def cli():
    parser=argparse.ArgumentParser()
    parser.add_argument("--config",type=Path,default=Path(__file__).parents[1]/"config/project.json")
    parser.add_argument("--data-root",type=Path)
    parser.add_argument("--output-root",type=Path)
    args, _=parser.parse_known_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    project_root=args.config.resolve().parent.parent
    data=args.data_root or project_root/"data"
    output=args.output_root or project_root/"output"
    main(args.config.resolve(),data.resolve(),output.resolve())

if __name__=="__main__": cli()
