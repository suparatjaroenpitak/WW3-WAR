"""Synthetic regression tests (not real GIS data) and a real DEM pixel-centre check."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'gis'))
from build_tile import *

class GISTests(unittest.TestCase):
    def test_axis_order_and_roundtrip(self):
        x,y=FORWARD.transform(100.579,14.36)
        self.assertTrue(600000<x<800000 and 1500000<y<1700000)
        lon,lat=INVERSE.transform(x,y)
        self.assertAlmostEqual(lon,100.579,9); self.assertAlmostEqual(lat,14.36,9)

    def test_polygon_hole_is_not_filled(self):
        poly=Polygon([(0,0),(20,0),(20,20),(0,20)],holes=[[(5,5),(15,5),(15,15),(5,15)]])
        verts,faces=surface_mesh(poly,lambda x,y:0,(0,0),grid=5)
        area=sum(Polygon([verts[i][:2] for i in f]).area for f in faces)
        self.assertAlmostEqual(area,300)

    def test_tile_seam_samples_are_identical(self):
        h=lambda x,y:.015*x+.03*y
        a,fa=surface_mesh(box(0,0,250,250),h,(0,0))
        b,fb=surface_mesh(box(250,0,500,250),h,(250,0))
        ea={(v[1],v[2]) for v in a if v[0]==250}
        eb={(v[1],v[2]) for v in b if v[0]==0}
        self.assertEqual(ea,eb); self.assertEqual(len(ea),21)

    def test_explicit_measurements(self):
        self.assertEqual(number('8 m'),8); self.assertAlmostEqual(number('10 ft'),3.048)
        self.assertIsNone(number('unknown')); self.assertIsNone(number('3;5'))

    def test_dem_pixel_centres(self):
        elevation=Elevation(box(670100,1588100,670600,1588600),save_crop=False)
        lon=elevation.x0+5*elevation.dx; lat=elevation.y0-5*elevation.dy
        x,y=FORWARD.transform(lon,lat)
        self.assertAlmostEqual(elevation.absolute(x,y),float(elevation.a[5,5]),6)

if __name__=='__main__': unittest.main()
