"""Fetch immutable source snapshots. Run only after reviewing source licences."""
from pathlib import Path
import datetime as dt
import hashlib
import json
import requests

ROOT = Path(__file__).resolve().parents[2]
DEM_KEY = 'Copernicus_DSM_COG_10_N14_00_E100_00_DEM'
DEM_BASE = f'https://copernicus-dem-30m.s3.amazonaws.com/{DEM_KEY}/'
SOURCES = {
    'osm': {'license': 'ODbL-1.0', 'license_url': 'https://opendatacommons.org/licenses/odbl/1-0/',
            'attribution': '© OpenStreetMap contributors', 'copyright_url': 'https://www.openstreetmap.org/copyright'},
    'dem': {'license': 'Copernicus DEM GLO-30 public licence', 'license_url': DEM_BASE+'INFO/eula_F.pdf',
            'attribution': 'produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved',
            'dataset_url': 'https://registry.opendata.aws/copernicus-dem/', 'type': 'DSM, NOT bare-earth DTM',
            'nominal_resolution_m': 30, 'vertical_datum': 'EGM2008', 'release': 'AWS 2021 release'}
}

def save_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')

def fetch(url, path, data=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    receipt = path.with_suffix(path.suffix+'.source.json')
    if path.exists() and receipt.exists():
        old = json.loads(receipt.read_text(encoding='utf-8'))
        if hashlib.sha256(path.read_bytes()).hexdigest() != old['sha256']:
            raise RuntimeError(f'Source modified: {path}')
        return old
    response = requests.get(url, params=data, headers={'User-Agent': 'Ayutthaya3D-Demo/0.1 (local GIS research)'}, timeout=(15, 180))
    response.raise_for_status()
    if data:
        obj = response.json()
        if obj.get('remark') or not obj.get('elements'):
            raise RuntimeError(f'Incomplete Overpass response: {obj.get("remark")}')
    path.write_bytes(response.content)
    meta = {'url': url, 'downloaded_at': dt.datetime.now(dt.timezone.utc).isoformat(),
            'downloaded_local_date': dt.datetime.now(dt.timezone(dt.timedelta(hours=7))).date().isoformat(),
            'sha256': hashlib.sha256(response.content).hexdigest(), 'bytes': len(response.content),
            'etag': response.headers.get('ETag'), 'last_modified': response.headers.get('Last-Modified')}
    save_json(receipt, meta)
    return meta

def main():
    for folder in ['raw','processed','metadata']:
        (ROOT/'data'/folder).mkdir(parents=True, exist_ok=True)
    licenses = ROOT/'data/metadata/licenses'
    fetch('https://www.openstreetmap.org/copyright', licenses/'osm_copyright.html')
    fetch(SOURCES['osm']['license_url'], licenses/'odbl.html')
    fetch(SOURCES['dem']['license_url'], licenses/'copernicus_eula.pdf')
    # Small survey window on the east side of Ayutthaya island, including the Pa Sak River.
    bbox = '14.349,100.566,14.370,100.586'
    query = f'''[out:json][timeout:120];(nwr[building]({bbox});way[highway]({bbox});nwr[natural=water]({bbox});nwr[waterway]({bbox});nwr[landuse]({bbox});nwr[leisure=park]({bbox});nwr[historic]({bbox}););out body geom;'''
    (ROOT/'data/raw/survey.overpassql').write_text(query, encoding='utf-8')
    failures = []
    for endpoint in ['https://overpass-api.de/api/interpreter', 'https://overpass.kumi.systems/api/interpreter']:
        try:
            SOURCES['osm']['download'] = fetch(endpoint, ROOT/'data/raw/osm_survey.json', {'data': query})
            break
        except Exception as exc:
            failures.append(str(exc)); print('Overpass retry:', exc, flush=True)
    else:
        raise RuntimeError(f'No usable OSM response: {failures}')
    print('OSM snapshot ready', flush=True)
    SOURCES['dem']['download'] = fetch(DEM_BASE+DEM_KEY+'.tif', ROOT/'data/raw/copernicus_n14_e100.tif')
    fetch(DEM_BASE+'Copernicus_DSM_10_N14_00_E100_00.xml', ROOT/'data/raw/copernicus_metadata.xml')
    save_json(ROOT/'data/metadata/sources.json', SOURCES)
    print('DEM snapshot ready', flush=True)

if __name__ == '__main__':
    main()
