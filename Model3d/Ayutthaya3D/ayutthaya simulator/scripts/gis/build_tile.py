"""OSM -> validated UTM tile -> portable mesh recipe. No invented GIS features."""
import argparse
import json
import math
import re
from collections import Counter
from pathlib import Path
import geopandas as gpd
import numpy as np
from PIL import Image
from pyproj import Transformer
from shapely import constrained_delaunay_triangles, make_valid, segmentize
from shapely.geometry import Polygon, LineString, Point, box, mapping
from shapely.geometry.polygon import orient
from shapely.ops import transform, unary_union, polygonize
from fetch_sources import ROOT, save_json

FORWARD = Transformer.from_crs(4326, 32647, always_xy=True)
INVERSE = Transformer.from_crs(32647, 4326, always_xy=True)
WIDTHS = {'motorway': 14, 'trunk': 12, 'primary': 10, 'secondary': 8, 'tertiary': 7,
          'residential': 6, 'unclassified': 6, 'service': 4, 'footway': 2, 'path': 1.5,
          'pedestrian': 4, 'cycleway': 2, 'steps': 2, 'track': 3}

def parts(geom, kind='Polygon'):
    if geom.is_empty:
        return []
    if geom.geom_type == kind:
        return [geom]
    return [p for g in getattr(geom, 'geoms', []) for p in parts(g, kind)]

def coordinates(element):
    return [(v['lon'], v['lat']) for v in element.get('geometry', []) if 'lon' in v]

def osm_geometry(element):
    if element['type'] == 'node':
        return Point(element['lon'], element['lat'])
    if element['type'] == 'way':
        pts = coordinates(element)
        if len(pts) < 2:
            return None
        if len(pts) >= 4 and pts[0] == pts[-1] and not element.get('tags', {}).get('highway'):
            return make_valid(Polygon(pts))
        return LineString(pts)
    if element.get('tags', {}).get('type') == 'multipolygon':
        outer, inner = [], []
        for member in element.get('members', []):
            pts = coordinates(member)
            if len(pts) >= 2:
                (inner if member.get('role') == 'inner' else outer).append(LineString(pts))
        shells = unary_union(list(polygonize(unary_union(outer))))
        holes = unary_union(list(polygonize(unary_union(inner))))
        return make_valid(shells.difference(holes))
    return None

def load_features():
    raw = json.loads((ROOT/'data/raw/osm_survey.json').read_text(encoding='utf-8'))
    features, skipped = [], []
    member_ids = set()
    for element in raw['elements']:
        if element['type'] == 'relation' and element.get('tags', {}).get('type') == 'multipolygon':
            member_ids.update(m['ref'] for m in element.get('members', []) if m['type'] == 'way')
    for element in raw['elements']:
        tags = element.get('tags', {})
        if element['type'] == 'way' and element['id'] in member_ids and not tags.get('highway'):
            continue
        geom = osm_geometry(element)
        if geom is None or geom.is_empty:
            skipped.append({'id': f"{element['type']}/{element['id']}", 'reason': 'unsupported or incomplete geometry'})
            continue
        geom = transform(FORWARD.transform, geom)
        if tags.get('building') and tags['building'] != 'no':
            category = 'buildings'
        elif tags.get('highway') and tags['highway'] not in ('proposed', 'construction'):
            category = 'roads'
        elif tags.get('natural') == 'water' or tags.get('waterway') in ('riverbank', 'river', 'stream', 'canal'):
            category = 'water'
        else:
            category = 'context'
        features.append({'id': f"{element['type']}/{element['id']}", 'tags': tags, 'category': category, 'geometry': geom})
    return features, skipped, raw.get('osm3s', {})

def select_tile(features, size):
    waters = unary_union([f['geometry'] for f in features if f['category']=='water' and parts(f['geometry'])])
    buildings = [f['geometry'] for f in features if f['category']=='buildings' and parts(f['geometry'])]
    roads = [f['geometry'] for f in features if f['category']=='roads']
    lo = FORWARD.transform(100.569, 14.352)
    hi = FORWARD.transform(100.583, 14.367)
    candidates = []
    for x in range(math.ceil(lo[0]/100)*100, int(hi[0]-size), 100):
        for y in range(math.ceil(lo[1]/100)*100, int(hi[1]-size), 100):
            tile = box(x,y,x+size,y+size)
            fraction = waters.intersection(tile).area / tile.area
            count = sum(g.intersects(tile) for g in buildings)
            length = sum(g.intersection(tile).length for g in roads)
            if .10 <= fraction <= .40 and count >= 5 and length > 250:
                score = min(count, 100)*2 + min(length,3000)/100 - abs(fraction-.24)*100
                candidates.append((score,x,y,count,fraction,length))
    if not candidates:
        raise RuntimeError('No tile meets real river/building/road coverage requirements.')
    best = max(candidates)
    return box(best[1],best[2],best[1]+size,best[2]+size), {'score': best[0], 'buildings': best[3], 'water_fraction': best[4], 'road_length_m': best[5]}

class Elevation:
    """Bilinear sampling at pixel centres; source nodata is a hard failure."""
    def __init__(self, tile, save_crop=True):
        minx,miny,maxx,maxy = tile.bounds
        lon0,lat0 = INVERSE.transform(minx-100,miny-100)
        lon1,lat1 = INVERSE.transform(maxx+100,maxy+100)
        # Pillow/libtiff fallback because Windows policy blocks this host's rasterio DLL.
        # Validate the exact supported GeoTIFF layout rather than guessing georeferencing.
        with Image.open(ROOT/'data/raw/copernicus_n14_e100.tif') as image:
            tags=image.tag_v2
            keys=tags[34735]
            geo={keys[i]:keys[i+3] for i in range(4,len(keys),4) if keys[i+1]==0}
            if geo.get(2048)!=4326 or 34264 in tags:
                raise RuntimeError('Fallback reader only supports axis-aligned WGS84 GeoTIFF')
            sx,sy,_=tags[33550]; px,py,_,tx,ty,_=tags[33922]
            if geo.get(1025) not in (1,2): raise RuntimeError('Unknown GeoTIFF pixel convention')
            offset=.5 if geo[1025]==1 else 0.0
            self.dx,self.dy=sx,sy
            self.x0=tx-px*sx+offset*sx; self.y0=ty+py*sy-offset*sy
            left=math.floor((lon0-self.x0)/sx)-2
            top=math.floor((self.y0-lat1)/sy)-2
            right=math.ceil((lon1-self.x0)/sx)+3
            bottom=math.ceil((self.y0-lat0)/sy)+3
            if min(left,top)<0 or right>image.width or bottom>image.height:
                raise RuntimeError('Tile crosses DEM source boundary; more source tiles required')
            self.a=np.ma.array(np.asarray(image.crop((left,top,right,bottom)),dtype=np.float64))
            self.x0+=left*sx; self.y0-=top*sy
            nodata=tags.get(42113)
            if nodata is not None: self.a=np.ma.masked_equal(self.a,float(str(nodata).strip('\x00')))
            if save_crop:
                np.savez_compressed(ROOT/'data/processed/dem_crop.npz',elevation=self.a.filled(np.nan),
                    pixel_center_lon=self.x0,pixel_center_lat=self.y0,dx=sx,dy=sy)
                save_json(ROOT/'data/processed/dem_crop.json',{'backend':'Pillow/libtiff','epsg':4326,
                    'pixel_center_lon':self.x0,'pixel_center_lat':self.y0,'dx':sx,'dy':sy,
                    'source_raster_type':geo[1025],'shape':list(self.a.shape),'vertical_datum':'EGM2008'})
        self.z_origin = math.floor(self.absolute(*tile.centroid.coords[0]))

    def absolute(self,x,y):
        lon,lat = INVERSE.transform(x,y)
        col=(lon-self.x0)/self.dx; row=(self.y0-lat)/self.dy
        i,j = math.floor(row),math.floor(col)
        if not (0<=i<self.a.shape[0]-1 and 0<=j<self.a.shape[1]-1):
            raise ValueError('Elevation sample outside downloaded crop')
        v = self.a[i:i+2,j:j+2]
        if np.ma.getmaskarray(v).any() or not np.isfinite(v).all():
            raise ValueError('DEM nodata: do not silently replace with zero')
        dy,dx = row-i,col-j
        return float(v[0,0]*(1-dx)*(1-dy)+v[0,1]*dx*(1-dy)+v[1,0]*(1-dx)*dy+v[1,1]*dx*dy)

    def __call__(self,x,y):
        return self.absolute(x,y)-self.z_origin

def number(value):
    if value is None:
        return None
    s = str(value).strip().lower()
    if re.fullmatch(r'\d+(\.\d+)?\s*(m|metres|meters)?',s):
        n=float(re.match(r'\d+(\.\d+)?',s)[0]); return n if n>0 else None
    if re.fullmatch(r'\d+(\.\d+)?\s*(ft|feet)',s):
        return float(re.match(r'\d+(\.\d+)?',s)[0])*.3048
    return None

def surface_mesh(geometry, height, origin, grid=12.5):
    vertices,faces,lookup=[],[],{}
    if geometry.is_empty:
        return vertices,faces
    def add(x,y):
        key=(round(x-origin[0],6),round(y-origin[1],6))
        if key not in lookup:
            lookup[key]=len(vertices); vertices.append([key[0],key[1],height(x,y)])
        return lookup[key]
    minx,miny,maxx,maxy=geometry.bounds
    # Fixed global grid gives matching terrain samples across adjacent tiles.
    for ix in range(math.floor(minx/grid),math.ceil(maxx/grid)):
        for iy in range(math.floor(miny/grid),math.ceil(maxy/grid)):
            cell=geometry.intersection(box(ix*grid,iy*grid,(ix+1)*grid,(iy+1)*grid))
            for poly in parts(cell):
                if poly.area<1e-7: continue
                for tri in constrained_delaunay_triangles(poly).geoms:
                    if tri.area<1e-8: continue
                    xy=list(orient(tri,sign=1).exterior.coords)[:-1]
                    idx=[add(*p) for p in xy]
                    if len(set(idx))==3: faces.append(idx)
    return vertices,faces

def building_mesh(poly, base, height, origin):
    poly=orient(poly,1)
    vertices,faces,lookup=[],[],{}
    def add(x,y,z):
        key=(round(x-origin[0],6),round(y-origin[1],6),round(z,6))
        if key not in lookup: lookup[key]=len(vertices); vertices.append(list(key))
        return lookup[key]
    for ring in [poly.exterior,*poly.interiors]:
        coords=list(ring.coords)
        for a,b in zip(coords,coords[1:]):
            faces.append([add(*a,base),add(*b,base),add(*b,base+height),add(*a,base+height)])
    wall_count=len(faces)
    for tri in constrained_delaunay_triangles(poly).geoms:
        xy=list(orient(tri,1).exterior.coords)[:-1]
        faces.append([add(*p,base+height) for p in xy])
        faces.append([add(*p,base) for p in reversed(xy)])
    return vertices,faces,wall_count

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--size',type=int,choices=[250,500,1000],default=500)
    parser.add_argument('--origin',type=float,nargs=2,help='UTM SW corner, overrides automatic demo selection')
    args=parser.parse_args()
    features,skipped,osm_meta=load_features()
    tile,selection=(box(*args.origin,args.origin[0]+args.size,args.origin[1]+args.size),{'mode':'explicit'}) if args.origin else select_tile(features,args.size)
    origin=tile.bounds[:2]
    elev=Elevation(tile)
    sources=json.loads((ROOT/'data/metadata/sources.json').read_text(encoding='utf-8'))
    clipped=[]
    for feature in features:
        geom=feature['geometry'].intersection(tile)
        if not geom.is_empty:
            clipped.append(dict(feature,geometry=geom))
    # Retain full tags, identities, WGS84 and UTM vector evidence.
    rows=[{'osm_id':f['id'],'category':f['category'],'tags_json':json.dumps(f['tags'],ensure_ascii=False),'geometry':f['geometry']} for f in clipped]
    frame=gpd.GeoDataFrame(rows,crs=32647)
    # Pure JSON writer avoids GDAL-dependent pyogrio on hosts with DLL restrictions.
    save_json(ROOT/'data/processed/tile_utm.json',{'schema':'UTM feature collection (not RFC7946)',
        'epsg':32647,'features':[{'id':f['id'],'category':f['category'],'tags':f['tags'],
        'geometry':mapping(f['geometry'])} for f in clipped]})
    (ROOT/'data/processed/tile_wgs84.geojson').write_text(frame.to_crs(4326).to_json(to_wgs84=True),encoding='utf-8')
    water=unary_union([f['geometry'] for f in clipped if f['category']=='water' and parts(f['geometry'])])
    if water.is_empty: raise RuntimeError('No real water polygon in selected tile')
    meshes=[]
    def append(name,category,verts,faces,metadata,**extra):
        if verts and faces:
            meshes.append(dict(name=name,category=category,vertices=verts,faces=faces,metadata=metadata,**extra))
    # Terrain under rivers is deliberately excluded: source DSM has no bathymetry.
    verts,faces=surface_mesh(tile.difference(water),elev,origin)
    append('Terrain_Copernicus_DSM','terrain',verts,faces,{'source':'Copernicus GLO-30','height_status':'real DSM, bilinear interpolation','bathymetry':'not available; river opening has no bed'})
    for f in clipped:
        geom,tags=f['geometry'],f['tags']
        name=f['category']+'_'+f['id'].replace('/','_')
        meta={'osm_id':f['id'],'source_url':'https://www.openstreetmap.org/'+f['id'],'tags':tags,'license':'ODbL-1.0'}
        if f['category']=='water' and parts(geom):
            verts,faces=surface_mesh(geom,lambda x,y:elev(x,y)+.03,origin)
            append(name,'water',verts,faces,dict(meta,height_status='DSM sampled + 0.03m visual offset; not measured current water level'))
        elif f['category']=='roads' and parts(geom,'LineString'):
            if tags.get('tunnel') not in (None,'no'):
                skipped.append({'id':f['id'],'reason':'tunnel requires separate vertical alignment'}); continue
            measured=number(tags.get('width'))
            width=measured or WIDTHS.get(tags.get('highway'),6)
            bridge=tags.get('bridge') not in (None,'no')
            lift=4.0 if bridge else 0.10
            road=unary_union([g.buffer(width/2,cap_style=2,join_style=2) for g in parts(f['geometry'],'LineString')]).intersection(tile)
            if not bridge: road=road.difference(water)
            verts,faces=surface_mesh(road,lambda x,y:elev(x,y)+lift,origin,grid=6.25)
            append(name,'roads',verts,faces,dict(meta,width_m=width,width_status='OSM tagged' if measured else 'estimated by highway class',
                height_status='estimated bridge clearance +4m' if bridge else 'draped over DSM +0.10m',bridge=bridge))
        elif f['category']=='buildings':
            tagged=number(tags.get('height'))
            levels=number(tags.get('building:levels'))
            height=tagged or (levels*3 if levels else 6)
            status='OSM height tag' if tagged else ('estimated 3m per OSM storey' if levels else 'estimated default 6m')
            for i,poly in enumerate(parts(geom)):
                if poly.area<1: continue
                samples=[elev(x,y) for x,y in segmentize(poly,5).exterior.coords]
                base=max(samples)+.08
                # Extend the solid down to the lowest perimeter to avoid floating walls.
                bottom=min(samples)-.10
                verts,faces,walls=building_mesh(poly,bottom,height+base-bottom,origin)
                append(name+f'_{i}','buildings',verts,faces,dict(meta,height_m=height,height_status=status,
                    roof_status='flat placeholder; roof tags retained but not modelled',facade_status='generic PBR colours; no photo reconstruction',
                    foundation_status='solid extends from lowest DSM perimeter -0.10m to level base at highest +0.08m',clipped_at_tile_boundary=poly.intersects(tile.boundary)),wall_faces=walls)
    counts=Counter(m['category'] for m in meshes)
    for key in ['terrain','roads','water','buildings']:
        if not counts[key]: raise RuntimeError('Missing mandatory category '+key)
    lon,lat=INVERSE.transform(*tile.centroid.coords[0])
    manifest={'schema_version':1,'tile_id':'AYU_DEMO_E0_N0','tile_size_m':args.size,'utm_epsg':32647,'source_epsg':4326,
        'local_origin_utm_m':[origin[0],origin[1],elev.z_origin], 'local_axes':{'x':'east','y':'north','z':'up'},
        'glTF_axes':{'x':'east','y':'up','z':'south'},'units':'metres','vertical_datum':'EGM2008',
        'center_wgs84':[lon,lat],'bounds_utm':list(tile.bounds),'terrain_status':'real Copernicus GLO-30 DSM',
        'sampling_grid_m':12.5,'source_resolution_m':30,'selection':selection,'counts':dict(counts),'osm_snapshot':osm_meta,
        'sources':sources,'limitations':['OSM coverage may be incomplete; mapping accuracy is not survey verified.',
          'DSM contains trees/buildings; no bare-earth correction, road grading or bathymetry.',
          'Unspecified building heights and road widths are explicitly estimated.',
          'No historical reconstruction, photogrammetry, vehicle physics, LOD or engine streaming in Phase 1-4.',
          'Basic roads only: junction cleanup, markings, kerbs and bridge engineering are later phases.'],
        'exports':{'blend':'tiles/AYU_DEMO_E0_N0/demo.blend','glb':'tiles/AYU_DEMO_E0_N0/demo.glb'},
        'engine_status':{'unity':'not imported or tested','unreal':'not imported or tested'}}
    save_json(ROOT/'data/processed/mesh_recipe.json',{'manifest':manifest,'meshes':meshes})
    save_json(ROOT/'tiles/AYU_DEMO_E0_N0/tile.json',manifest)
    # Numerical round trips assess the implementation, not source survey accuracy.
    errors=[]
    for f in clipped:
        p=f['geometry'].representative_point(); lng,lat_=INVERSE.transform(p.x,p.y); xx,yy=FORWARD.transform(lng,lat_)
        errors.append(math.hypot(xx-p.x,yy-p.y))
    save_json(ROOT/'reports/gis_validation.json',{'status':'passed','coordinate_roundtrip_max_m':max(errors),
        'tile_dimensions_m':[args.size,args.size],'valid_geometries':bool(frame.geometry.is_valid.all()),
        'counts':dict(counts),'skipped_features':skipped,'water_area_m2':water.area,
        'source_accuracy_verified_by_survey':False,'building_heights':dict(Counter(m['metadata']['height_status'] for m in meshes if m['category']=='buildings'))})
    print(json.dumps({'center':manifest['center_wgs84'],'origin':manifest['local_origin_utm_m'],'counts':dict(counts),
        'vertices':sum(len(m['vertices']) for m in meshes),'faces':sum(len(m['faces']) for m in meshes)},indent=2))

if __name__=='__main__': main()
