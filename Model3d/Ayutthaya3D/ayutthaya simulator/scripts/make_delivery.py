"""Create review preview, phase report and portable delivery manifest from evidence."""
import datetime as dt
import hashlib
import importlib
import json
import platform
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1]
def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def write(p,value):
    path=ROOT/p; path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,indent=2,ensure_ascii=False),encoding='utf-8')

def main():
    for d in ['scripts/workers','scripts/exporters','assets/terrain','assets/roads','assets/buildings',
              'assets/historical','assets/vegetation','assets/props','exports/unity','exports/unreal','logs']:
        (ROOT/d).mkdir(parents=True,exist_ok=True)
    tile=read('tiles/AYU_DEMO_E0_N0/tile.json')
    glb=read('reports/glb_validation.json'); geo=read('reports/gis_validation.json'); rt=read('reports/export_roundtrip.json')
    deps={}
    for name in ['geopandas','pyproj','shapely','rasterio','requests','numpy','PIL','pyogrio']:
        try:
            module=importlib.import_module(name); deps[name]={'status':'usable','version':module.__version__}
        except Exception as exc:
            deps[name]={'status':'installed_but_import_blocked','error':str(exc)}
    now=dt.datetime.now(dt.timezone(dt.timedelta(hours=7))).isoformat()
    report={'generated_at':now,'scope':'Phase 1-4; ONE 500x500m demo tile only',
        'overall':'delivered_with_documented_dependency_workaround',
        'phases':[
            {'phase':1,'status':'passed','evidence':'Blender MCP get_addon_status / get_scene_info / execute_blender_code',
             'blender':'5.2.2 LTS','mcp_addon':'1.8.8','mcp_protocol':13,'telemetry':False,
             'note':'game-dev CLI unavailable; native Blender and project validators used. Live original scene preserved.'},
            {'phase':2,'status':'completed_with_workaround','python':platform.python_version(),'dependencies':deps,
             'workaround':'Pillow/libtiff reads checked WGS84 GeoTIFF tags; GeoPandas uses JSON output without GDAL. Windows security policy unchanged.'},
            {'phase':3,'status':geo['status'],'evidence':'reports/gis_validation.json',
             'sources':'OpenStreetMap ODbL-1.0 and Copernicus GLO-30 public licence',
             'crs':'WGS84 -> EPSG:32647 -> local metre origin','raw_sources_retained':True},
            {'phase':4,'status':'passed' if glb['status']==rt['status']=='passed' else 'failed',
             'evidence':['reports/blender_validation.json','reports/glb_validation.json','reports/export_roundtrip.json'],
             'counts':tile['counts'],'mesh_nodes':glb['mesh_nodes'],'triangles':glb['triangles'],'tile_size_m':tile['tile_size_m']}],
        'limitations':tile['limitations'],'future_phases':'5-12 are not claimed complete; Unity and Unreal have not been tested.'}
    write('reports/phase_report.json',report)
    files={}
    for p in [ROOT/'tiles/AYU_DEMO_E0_N0/demo.blend',ROOT/'tiles/AYU_DEMO_E0_N0/demo.glb',ROOT/'tiles/AYU_DEMO_E0_N0/tile.json',
              ROOT/'data/processed/mesh_recipe.json',ROOT/'data/processed/tile_wgs84.geojson']:
        files[str(p.relative_to(ROOT)).replace('\\','/')]={'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
    write('delivery_manifest.json',{'schema_version':1,'created_at':now,'files':files,'sources':'data/metadata/sources.json',
        'attribution':'ATTRIBUTION.md','validation':'reports/phase_report.json',
        'tested':'Blender 5.2.2 reopen and glTF import; GLB binary inspection','engine_tested':False})
    for engine in ['unity','unreal']:
        write(f'exports/{engine}/tile_manifest.json',{'status':'prepared metadata only; engine import not tested',
            'tile_id':tile['tile_id'],'tile_size_m':tile['tile_size_m'],'source':'../../tiles/AYU_DEMO_E0_N0/demo.glb',
            'origin_utm_m':tile['local_origin_utm_m'],'epsg':32647,
            'basis_from_blender':{'x':'east','y':'up','z':'north','unit':'metre'} if engine=='unity' else {'x':'north','y':'east','z':'up','unit':'centimetre'},
            'streaming':'NOT implemented; importing a GLB does not enable streaming',
            'collision':'Configure and test engine mesh colliders separately','lod':'not implemented in Phase 1-4'})
    src=Image.open(ROOT/'previews/scene_raw.png').convert('RGB')
    canvas=Image.new('RGB',(1400,1480),'#14232c'); canvas.paste(src,(0,155))
    draw=ImageDraw.Draw(canvas)
    font_path='C:/Windows/Fonts/segoeui.ttf'; bold_path='C:/Windows/Fonts/segoeuib.ttf'
    def font(n,bold=False): return ImageFont.truetype(bold_path if bold else font_path,n)
    draw.text((48,24),'AYUTTHAYA',font=font(46,True),fill='#f0eee3')
    draw.text((49,86),'PA SAK RIVER  /  REAL GIS DEMO',font=font(22),fill='#a8c9d3')
    draw.text((900,36),'500 × 500 m  |  SCALE 1:1',font=font(25,True),fill='#f0eee3')
    draw.text((900,77),'UTM 47N  •  PHASES 1–4',font=font(20),fill='#a8c9d3')
    draw.text((48,1335),f"{tile['counts']['buildings']} BUILDING PARTS     {tile['counts']['roads']} ROAD / PATH PARTS     {glb['triangles']:,} TRIANGLES",font=font(21,True),fill='#f0eee3')
    draw.text((48,1372),'Real footprints + river outlines. Building heights, roofs and materials are approximations.',font=font(19),fill='#b8c7cb')
    draw.text((48,1405),'© OpenStreetMap contributors / ODbL  •  Terrain: Copernicus GLO-30 DSM, nominal 30 m.',font=font(18),fill='#b8c7cb')
    draw.text((48,1435),'Full Copernicus credits and licence notices: ATTRIBUTION.md. No engine gameplay test.',font=font(17),fill='#91a5af')
    canvas.save(ROOT/'previews/ayutthaya_demo.png')
    print(json.dumps({'phase_report':report['overall'],'triangles':glb['triangles'],'files':list(files)},indent=2))

if __name__=='__main__': main()
