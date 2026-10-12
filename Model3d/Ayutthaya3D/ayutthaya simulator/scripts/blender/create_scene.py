"""Run with Blender --background --factory-startup --python this_file -- --root ROOT.

Consumes a pure JSON mesh recipe; no GIS dependencies inside Blender.
"""
import argparse
import json
import math
import sys
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector

def enum(operator, prop, preferred):
    values=[v.identifier for v in operator.get_rna_type().properties[prop].enum_items]
    if not values and prop=='export_format':
        from io_scene_gltf2 import get_format_items
        values=[v[0] for v in get_format_items(bpy.context.scene,bpy.context)]
    if preferred not in values: raise RuntimeError(f'{prop}: {preferred} unavailable; {values}')
    return preferred

def material(name,color,roughness):
    mat=bpy.data.materials.new(name)
    mat.use_nodes=True
    bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value=(*color,1)
    bsdf.inputs['Roughness'].default_value=roughness
    mat.diffuse_color=(*color,1)
    return mat

def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()

def main():
    p=argparse.ArgumentParser(); p.add_argument('--root',required=True)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    root=Path(args.root)
    data=json.loads((root/'data/processed/mesh_recipe.json').read_text(encoding='utf-8'))
    manifest=data['manifest']; size=manifest['tile_size_m']
    for folder in ['tiles/AYU_DEMO_E0_N0','previews','reports']:
        (root/folder).mkdir(parents=True,exist_ok=True)
    # This script runs in a separate background process, never in the user's live scene.
    bpy.ops.object.select_all(action=enum(bpy.ops.object.select_all,'action','SELECT'))
    bpy.ops.object.delete(use_global=False)
    scene=bpy.context.scene
    scene.name='Ayutthaya_Demo_500m'
    scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1
    scene['tile_manifest']=json.dumps(manifest,ensure_ascii=False)
    scene['attribution']='© OpenStreetMap contributors; produced using Copernicus WorldDEM-30. See ATTRIBUTION.md'
    mats={
        'terrain':material('Ground_DSM_generic_olive',(0.27,.32,.16),.92),
        'roads':material('Road_generic_asphalt',(.12,.15,.16),.9),
        'water':material('River_generic_blue',(.045,.22,.29),.28),
        'buildings':material('Walls_generic_limestone',(.66,.62,.48),.8),
        'roof':material('Roof_placeholder_terracotta',(.37,.14,.08),.85)}
    collections={}
    for category in mats:
        if category=='roof': continue
        col=bpy.data.collections.new(category.title()); scene.collection.children.link(col); collections[category]=col
    objects=[]; reports=[]
    for spec in data['meshes']:
        mesh=bpy.data.meshes.new(spec['name']+'_Mesh')
        mesh.from_pydata(spec['vertices'],[],spec['faces']); mesh.update()
        if mesh.validate(verbose=False):
            raise RuntimeError('Blender repaired unexpected invalid input mesh: '+spec['name'])
        obj=bpy.data.objects.new(spec['name'],mesh); collections[spec['category']].objects.link(obj)
        obj.data.materials.append(mats[spec['category']])
        if spec['category']=='buildings':
            obj.data.materials.append(mats['roof'])
            for face in mesh.polygons:
                if face.normal.z>.5: face.material_index=1
        uv=mesh.uv_layers.new(name='UVMap')
        # Metre-based box projection gives nondegenerate UVs to walls as well as roofs.
        for face in mesh.polygons:
            n=face.normal
            for loop_idx in face.loop_indices:
                v=mesh.vertices[mesh.loops[loop_idx].vertex_index].co
                uv.data[loop_idx].uv=(v.y/10,v.z/10) if abs(n.x)>.5 else ((v.x/10,v.z/10) if abs(n.y)>.5 else (v.x/10,v.y/10))
        obj['category']=spec['category']
        for key,value in spec['metadata'].items():
            obj[key]=value if isinstance(value,(str,int,float,bool)) else json.dumps(value,ensure_ascii=False)
        obj['origin_utm']=manifest['local_origin_utm_m']
        obj['placement_status']='GIS-derived; see metadata for estimated dimensions'
        objects.append(obj)
        bm=bmesh.new(); bm.from_mesh(mesh)
        boundary=sum(e.is_boundary for e in bm.edges)
        bad=sum(not e.is_manifold for e in bm.edges)
        degenerate=sum(f.calc_area()<1e-8 for f in bm.faces)
        bm.free()
        if degenerate: raise RuntimeError(f'Degenerate faces: {obj.name}: {degenerate}')
        if spec['category']=='buildings' and bad: raise RuntimeError('Non-watertight building: '+obj.name)
        reports.append({'name':obj.name,'category':spec['category'],'vertices':len(mesh.vertices),'polygons':len(mesh.polygons),
            'triangles':sum(len(f.vertices)-2 for f in mesh.polygons),'boundary_edges':boundary,'non_manifold_edges':bad,
            'uv_maps':len(mesh.uv_layers),'materials':len(mesh.materials),'degenerate_faces':degenerate})
    # Export meshes only; glTF applies Blender Z-up -> glTF Y-up conversion.
    bpy.ops.object.select_all(action=enum(bpy.ops.object.select_all,'action','DESELECT'))
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    out=root/'tiles/AYU_DEMO_E0_N0'
    bpy.ops.export_scene.gltf(filepath=str(out/'demo.glb'),export_format=enum(bpy.ops.export_scene.gltf,'export_format','GLB'),
        use_selection=True,export_extras=True,export_yup=True,export_texcoords=True,export_normals=True)
    # Cameras and lighting are Blender preview helpers, not part of the exported map.
    cam_data=bpy.data.cameras.new('SurveyCamera'); cam=bpy.data.objects.new('SurveyCamera',cam_data); scene.collection.objects.link(cam)
    cam.location=(size*1.22,-size*.72,size*1.25); aim(cam,(size*.5,size*.5,0))
    cam_data.type='ORTHO'; cam_data.ortho_scale=size*1.48; cam_data.clip_end=5000; scene.camera=cam
    sun_data=bpy.data.lights.new('Daylight','SUN'); sun=bpy.data.objects.new('Daylight',sun_data); scene.collection.objects.link(sun)
    sun.rotation_euler=(math.radians(24),math.radians(-20),math.radians(-25)); sun_data.energy=2.4; sun_data.angle=.12
    world=bpy.data.worlds.new('SurveyWorld'); scene.world=world; world.use_nodes=True
    background=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND'); background.inputs['Color'].default_value=(.12,.16,.20,1); background.inputs['Strength'].default_value=.7
    current=scene.render.engine
    try: scene.render.engine='BLENDER_EEVEE'
    except TypeError as exc: raise RuntimeError(f'EEVEE unavailable: current={current}, {exc}')
    scene.render.resolution_x=1400; scene.render.resolution_y=1200; scene.render.resolution_percentage=100
    scene.render.threads_mode='FIXED'; scene.render.threads=2
    scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
    scene.render.filepath=str(root/'previews/scene_raw.png')
    # Store a usable starting viewport in the delivered .blend.
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.clip_end=5000
                area.spaces.active.region_3d.view_distance=size*1.4
                area.spaces.active.region_3d.view_location=(size*.5,size*.5,0)
                area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
                area.spaces.active.shading.color_type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(out/'demo.blend'))
    all_vertices=[v for m in data['meshes'] for v in m['vertices']]
    report={'status':'passed','blender_version':bpy.app.version_string,'unit_scale':scene.unit_settings.scale_length,
            'bounds_min':[min(v[i] for v in all_vertices) for i in range(3)],
            'bounds_max':[max(v[i] for v in all_vertices) for i in range(3)],'meshes':reports,
            'total_triangles':sum(r['triangles'] for r in reports),'textures':'No external images; constant PBR materials',
            'collision_status':'terrain surface and watertight building meshes available; engine collision not configured',
            'lod_status':'not implemented in Phase 1-4'}
    (root/'reports/blender_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    bpy.ops.render.render(write_still=True)
    print('AYUTTHAYA_BUILD_COMPLETE',json.dumps({'objects':len(objects),'triangles':report['total_triangles']}))

if __name__=='__main__': main()
