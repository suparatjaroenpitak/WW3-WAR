import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

from pyproj import Transformer
from shapely.geometry import box

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("download_tile", ROOT / "scripts" / "download_tile.py")
downloader = importlib.util.module_from_spec(spec)
spec.loader.exec_module(downloader)


class PipelineTests(unittest.TestCase):
    def test_project_uses_metric_local_origin_and_real_sources(self):
        cfg = json.loads((ROOT / "config/project.json").read_text(encoding="utf-8"))
        p = cfg["project"]
        self.assertEqual(p["meters_per_blender_unit"], 1.0)
        self.assertEqual(p["projected_crs"], "EPSG:32647")
        self.assertAlmostEqual(p["local_origin"]["latitude"], 14.3535427)
        self.assertIn("nominatim.openstreetmap.org", p["local_origin"]["source_url"])
        self.assertIn("Copernicus", cfg["sources"]["dem"]["provider"])
        self.assertEqual(cfg["sources"]["osm"]["license"], "ODbL 1.0")

    def test_osm_feature_reprojection_is_local_to_origin(self):
        origin = (100.5645684, 14.3535427)
        projected = Transformer.from_crs("EPSG:4326", "EPSG:32647", always_xy=True)
        x0,y0=projected.transform(*origin)
        raw={"elements":[{"type":"node","id":1,"lon":origin[0],"lat":origin[1]},{"type":"node","id":2,"lon":origin[0]+0.0001,"lat":origin[1]},{"type":"way","id":99,"nodes":[1,2],"tags":{"highway":"residential"}}]}
        with tempfile.TemporaryDirectory() as tmp:
            source=Path(tmp)/"raw.json"; dest=Path(tmp)/"out.geojson"
            source.write_text(json.dumps(raw),encoding="utf-8")
            count=downloader.osm_to_geojson(source,dest,projected,(x0,y0),box(-500,-500,500,500))
            doc=json.loads(dest.read_text(encoding="utf-8"))
        self.assertEqual(count,1)
        coords=doc["features"][0]["geometry"]["coordinates"]
        self.assertAlmostEqual(coords[0][0],0.0,places=2)
        self.assertGreater(coords[1][0],0)
        self.assertEqual(doc["features"][0]["properties"]["tags"]["highway"],"residential")

    def test_incomplete_data_never_gets_synthetic_fallback(self):
        source=(ROOT/"blender"/"build_demo.py").read_text(encoding="utf-8")
        self.assertIn("refusing to build a fabricated surface",source)
        source=(ROOT/"scripts"/"download_tile.py").read_text(encoding="utf-8")
        self.assertIn("no synthetic fill permitted",source)
        self.assertIn("valid.sum()) < arr.size*0.95",source)
        self.assertIn("ModelTiepointTag",source)

    def test_demo_extent_is_exactly_one_square_kilometre(self):
        cfg=json.loads((ROOT/"config/project.json").read_text(encoding="utf-8"))
        x0,y0,x1,y1=cfg["project"]["tile_extent_local_m"]
        self.assertEqual((x1-x0)*(y1-y0),1_000_000)

if __name__=="__main__":
    unittest.main()
