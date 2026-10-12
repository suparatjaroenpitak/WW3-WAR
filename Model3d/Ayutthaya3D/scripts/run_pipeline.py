"""Bounded sequential demo build with checkpoint hashes; full worker system is Phase 8."""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]
def digest(paths):
    h=hashlib.sha256()
    for p in sorted(paths):
        h.update(str(p).encode()); h.update(p.read_bytes())
    return h.hexdigest()

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--blender',default='C:/Program Files/Blender Foundation/Blender 5.2/blender.exe')
    p.add_argument('--offline',action='store_true',help='Use retained raw sources, no network calls')
    p.add_argument('--resume',action='store_true',help='Skip only stages whose input AND output hashes still match')
    args=p.parse_args()
    if not Path(args.blender).is_file(): p.error('Blender executable not found')
    (ROOT/'logs').mkdir(exist_ok=True)
    py=sys.executable
    blender=[args.blender,'--background','--factory-startup','--threads','2','--python-exit-code','1']
    stages=[]
    if not args.offline:
        stages.append(('fetch',[py,str(ROOT/'scripts/gis/fetch_sources.py')],
            ['scripts/gis/fetch_sources.py'],['data/raw/osm_survey.json','data/raw/copernicus_n14_e100.tif','data/metadata/sources.json']))
    stages.extend([
        ('gis',[py,str(ROOT/'scripts/gis/build_tile.py')],['scripts/gis/build_tile.py','scripts/gis/fetch_sources.py','data/raw/osm_survey.json','data/raw/copernicus_n14_e100.tif','data/metadata/sources.json'],
         ['data/processed/mesh_recipe.json','tiles/AYU_DEMO_E0_N0/tile.json','reports/gis_validation.json']),
        ('blender',blender+['--python',str(ROOT/'scripts/blender/create_scene.py'),'--','--root',str(ROOT)],
         ['scripts/blender/create_scene.py','data/processed/mesh_recipe.json'],['tiles/AYU_DEMO_E0_N0/demo.blend','tiles/AYU_DEMO_E0_N0/demo.glb','reports/blender_validation.json','previews/scene_raw.png']),
        ('roundtrip',blender+['--python',str(ROOT/'scripts/validators/verify_exports_blender.py'),'--',str(ROOT)],
         ['scripts/validators/verify_exports_blender.py','tiles/AYU_DEMO_E0_N0/demo.blend','tiles/AYU_DEMO_E0_N0/demo.glb','reports/blender_validation.json'],['reports/export_roundtrip.json']),
        ('glb',[py,str(ROOT/'scripts/validators/validate_glb.py')],
         ['scripts/validators/validate_glb.py','tiles/AYU_DEMO_E0_N0/demo.glb','reports/blender_validation.json'],['reports/glb_validation.json']),
        ('tests',[py,str(ROOT/'scripts/validators/test_gis.py'),'-v'],
         ['scripts/validators/test_gis.py','scripts/gis/build_tile.py'],[]),
        ('delivery',[py,str(ROOT/'scripts/make_delivery.py')],
         ['scripts/make_delivery.py','reports/glb_validation.json','reports/export_roundtrip.json','reports/gis_validation.json','previews/scene_raw.png'],
         ['reports/phase_report.json','delivery_manifest.json','previews/ayutthaya_demo.png'])])
    state_path=ROOT/'logs/pipeline_state.json'
    state=json.loads(state_path.read_text()) if state_path.exists() else {}
    for name,command,inputs,outputs in stages:
        input_hash=digest([ROOT/x for x in inputs]); previous=state.get(name,{})
        output_paths=[ROOT/x for x in outputs]
        # Validation stages always rerun, including source checksum verification.
        if name not in ('glb','tests','delivery') and args.resume and previous.get('input_hash')==input_hash and all(x.exists() for x in output_paths) and previous.get('output_hash')==digest(output_paths):
            print(name+': cached and verified',flush=True); continue
        print(name+': running (MAX_WORKERS=1)',flush=True)
        env=os.environ.copy(); env['PYTHONIOENCODING']='utf-8'; env['OMP_NUM_THREADS']='2'
        with (ROOT/f'logs/{name}.log').open('w',encoding='utf-8') as log:
            result=subprocess.run(command,cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT,
                timeout=600,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
        state[name]={'status':'passed' if result.returncode==0 else 'failed','input_hash':input_hash,
            'output_hash':digest(output_paths) if result.returncode==0 else None,
            'finished_at':dt.datetime.now(dt.timezone.utc).isoformat()}
        state_path.write_text(json.dumps(state,indent=2),encoding='utf-8')
        if result.returncode: raise RuntimeError(f'{name} failed: see logs/{name}.log')
    print('Demo pipeline complete. See reports/phase_report.json',flush=True)

if __name__=='__main__': main()
