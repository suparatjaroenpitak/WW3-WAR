"""Independently reopen BLEND, then import GLB in the same clean background process."""
import bpy
import json
import sys
from pathlib import Path

root=Path(sys.argv[sys.argv.index('--')+1])
reference=json.loads((root/'reports/blender_validation.json').read_text())
target=root/'tiles/AYU_DEMO_E0_N0'
results={}
for kind in ['blend','glb']:
    if kind=='blend':
        bpy.ops.wm.open_mainfile(filepath=str(target/'demo.blend'))
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(target/'demo.glb'))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    coords=[o.matrix_world @ v.co for o in objects for v in o.data.vertices]
    bounds_min=[min(v[i] for v in coords) for i in range(3)]
    bounds_max=[max(v[i] for v in coords) for i in range(3)]
    error=max(abs(a-b) for key,values in [('bounds_min',bounds_min),('bounds_max',bounds_max)] for a,b in zip(values,reference[key]))
    checks={'object_count':len(objects)==len(reference['meshes']), 'metre_bounds_preserved':error<.002,
        'all_uvs_present':all(o.data.uv_layers for o in objects),
        'all_materials_present':all(o.data.materials for o in objects),
        'source_metadata_present':all('category' in o for o in objects)}
    results[kind]={'checks':checks,'mesh_count':len(objects),'bounds_error_m':error,'bounds_min':bounds_min,'bounds_max':bounds_max}
passed=all(all(v['checks'].values()) for v in results.values())
report={'status':'passed' if passed else 'failed','method':'fresh Blender reopen + glTF import','results':results,'engine_import_tested':False}
(root/'reports/export_roundtrip.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
if not passed: raise RuntimeError('Export verification failed')
