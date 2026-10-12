"""Validate GLB bytes, accessor data, UVs, materials, provenance and source hashes."""
import hashlib
import json
import struct
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
def validate():
    path=ROOT/'tiles/AYU_DEMO_E0_N0/demo.glb'; raw=path.read_bytes()
    magic,version,length=struct.unpack_from('<4sII',raw)
    assert (magic,version,length)==(b'glTF',2,len(raw)), 'Invalid GLB header'
    offset=12; chunks=[]
    while offset<len(raw):
        n,kind=struct.unpack_from('<II',raw,offset); offset+=8
        assert offset+n<=len(raw) and n%4==0, 'Invalid chunk length'
        chunks.append((kind,raw[offset:offset+n])); offset+=n
    assert len(chunks)==2 and chunks[0][0]==0x4E4F534A and chunks[1][0]==0x004E4942
    gltf=json.loads(chunks[0][1]); binary=chunks[1][1]
    assert len(gltf['buffers'])==1 and 'uri' not in gltf['buffers'][0]
    assert gltf['buffers'][0]['byteLength']<=len(binary)
    types={5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}
    dims={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}
    def accessor(index):
        a=gltf['accessors'][index]; v=gltf['bufferViews'][a['bufferView']]
        dtype=np.dtype(types[a['componentType']]); dim=dims[a['type']]
        stride=v.get('byteStride',dtype.itemsize*dim)
        start=v.get('byteOffset',0)+a.get('byteOffset',0)
        end=start+(a['count']-1)*stride+dim*dtype.itemsize
        assert end<=v.get('byteOffset',0)+v['byteLength']<=len(binary)
        return np.ndarray((a['count'],dim),dtype=dtype,buffer=binary,offset=start,strides=(stride,dtype.itemsize))
    triangles=0; primitives=0
    for mesh in gltf['meshes']:
        for p in mesh['primitives']:
            primitives+=1; assert p.get('mode',4)==4
            attrs=p['attributes']; assert {'POSITION','NORMAL','TEXCOORD_0'}<=attrs.keys()
            xyz=accessor(attrs['POSITION']); uv=accessor(attrs['TEXCOORD_0']); normal=accessor(attrs['NORMAL'])
            assert np.isfinite(xyz).all() and np.isfinite(uv).all() and np.isfinite(normal).all()
            assert len(xyz)==len(uv)==len(normal)
            idx=accessor(p['indices']).ravel(); assert len(idx)%3==0 and idx.max()<len(xyz)
            t=xyz[idx.reshape(-1,3)].astype(float)
            areas=np.linalg.norm(np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]),axis=1)*.5
            assert (areas>1e-8).all(), 'Zero-area exported triangle'
            assert 0<=p['material']<len(gltf['materials'])
            triangles+=len(idx)//3
    meshes=[n for n in gltf['nodes'] if 'mesh' in n]
    assert all(n.get('extras',{}).get('category') for n in meshes)
    assert not gltf.get('images'), 'Unexpected external texture dependency'
    hashes=[]
    for receipt in (ROOT/'data').rglob('*.source.json'):
        source=Path(str(receipt).removesuffix('.source.json'))
        expected=json.loads(receipt.read_text(encoding='utf-8'))
        actual=hashlib.sha256(source.read_bytes()).hexdigest()
        assert actual==expected['sha256'],f'Source changed: {source}'
        hashes.append({'file':str(source.relative_to(ROOT)),'sha256':actual})
    expected=json.loads((ROOT/'reports/blender_validation.json').read_text())
    assert triangles==expected['total_triangles']
    report={'status':'passed','file':str(path.relative_to(ROOT)),'bytes':len(raw),
        'sha256':hashlib.sha256(raw).hexdigest(),'mesh_nodes':len(meshes),'primitives':primitives,
        'triangles':triangles,'external_textures':0,'materials':len(gltf['materials']),
        'source_hashes_verified':hashes,'checks':['GLB header/chunks','accessor buffer bounds','finite vertices and UVs',
        'triangle areas','index ranges','material references','source metadata','source file hashes','triangle count after export']}
    (ROOT/'reports/glb_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='source_hashes_verified'},indent=2))
    return report
if __name__=='__main__': validate()
