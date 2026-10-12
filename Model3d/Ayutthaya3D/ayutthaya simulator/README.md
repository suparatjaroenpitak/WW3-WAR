# Ayutthaya 3D Open World — Demo Phase 1–4

Demo จริงจาก GIS ขนาด **500 × 500 เมตร** บริเวณแม่น้ำป่าสัก จังหวัดพระนครศรีอยุธยา
จุดกึ่งกลาง: **14.3620456333° N, 100.5797776080° E**

## เปิดผลลัพธ์

- `tiles/AYU_DEMO_E0_N0/demo.blend` — ฉาก Blender มีคอลเลกชัน Terrain, Roads, Water, Buildings, กล้องและแสง
- `tiles/AYU_DEMO_E0_N0/demo.glb` — โมเดลรวม 610 Mesh, 13,824 สามเหลี่ยม, 5 วัสดุ PBR ไม่มีไฟล์ texture ภายนอก
- `tiles/AYU_DEMO_E0_N0/tile.json` — พิกัด จุดกำเนิด แหล่งข้อมูล และข้อจำกัด
- `previews/ayutthaya_demo.png` — ภาพเรนเดอร์ตรวจงาน
- `reports/phase_report.json` — ผลราย Phase
- `delivery_manifest.json` — ขนาดไฟล์และ SHA-256

## ข้อมูลจริงและค่าประมาณ

| องค์ประกอบ | ข้อมูลที่ใช้ | ข้อจำกัด |
|---|---|---|
| Terrain | Copernicus GLO-30 DSM จริง ความละเอียดประมาณ 30 ม. | รวมยอดต้นไม้/อาคาร ไม่ใช่ bare-earth DTM; ใช้ bilinear interpolation ไม่เพิ่มความแม่นยำต้นฉบับ |
| อาคาร 513 ชิ้น | Polygon ฐานอาคารจาก OpenStreetMap | ทุกชิ้นขาด height/levels จึงใช้ความสูงประมาณ 6 ม.; หลังคาแบนและสีวัสดุเป็น Placeholder |
| ถนน/ทางเดิน 91 ชิ้น | แนวเส้นและประเภทจาก OSM | ใช้ width tag เมื่ออ่านได้ มิฉะนั้นประมาณตามประเภท; พื้นถนนตาม DSM ยังไม่มี grading/intersection cleanup |
| พื้นที่น้ำ 5 ชิ้น | Polygon น้ำจาก OSM | ผิวน้ำใช้ DSM +0.03 ม. เพื่อแสดงผล ไม่ใช่ระดับน้ำปัจจุบัน; ไม่สร้างความลึกท้องน้ำขึ้นเอง |
| วัสดุ/UV | วัสดุ PBR สีคงที่และ UV แบบ box projection | ไม่มีภาพถ่ายหรือ photogrammetry และไม่ได้อ้างว่าพื้นผิวเหมือนจริง |

จำนวนเป็น **ชิ้น geometry ที่ตัดใน Tile** ไม่ใช่ผลสำรวจจำนวนอาคาร/ถนนทั้งหมดในพื้นที่
เส้นแบ่ง Tile อาจตัดอาคาร โมเดลที่ถูกตัดมี `clipped_at_tile_boundary` ใน metadata
ฐานอาคารต่อเนื่องลงถึงค่าต่ำสุดของ DSM รอบอาคารเพื่อไม่ให้ลอย แต่ DSM ยังอาจมีสิ่งปลูกสร้างเดิมปนอยู่
ความแม่นยำแหล่งข้อมูลไม่ได้ตรวจด้วยหมุดสำรวจภาคสนาม

## พิกัดและสเกล

- ต้นทาง WGS84 (EPSG:4326), คำนวณด้วย UTM Zone 47N (EPSG:32647), `always_xy=True`
- Local Origin = **E 670100 m, N 1588100 m, Z 6 m** โดย Z อ้างอิง EGM2008
- Blender: X ตะวันออก, Y เหนือ, Z ขึ้น; 1 Unit = 1 เมตร
- พื้นที่ Local XY = [0,500] × [0,500]
- GLB หลัง exporter: X ตะวันออก, Y ขึ้น, Z ใต้ (Y-up ตาม glTF)
- `E = origin_E + Blender_X`, `N = origin_N + Blender_Y`, `height_EGM2008 = origin_Z + Blender_Z`

## สถานะ Phase

1. **ผ่านสำหรับ OpenCode:** ตรวจและเรียก Blender MCP ผ่าน OpenCode CLI 2.0.27 / Desktop 2.0.22 กับ Blender 5.2.2 LTS; addon รายงานเวอร์ชัน 1.8 / protocol 13. ตรวจ `get_addon_status`, `get_scene_info` และคำสั่ง Blender Python แบบอ่านอย่างเดียวแล้ว. Codex มี MCP server `blender` ตั้งค่าไว้แล้วและคำสั่ง compatibility ใช้งานได้ แต่ยังไม่ได้ทดสอบเรียก tool จากหน้าต่าง Codex Desktop โดยตรง
2. **ผ่านโดยมีวิธีสำรอง:** ติดตั้ง geopandas, pyproj, shapely, rasterio, requests และ pyogrio ใน `.venv`; ปัจจุบัน pyogrio/GDAL 3.12.4 ใช้งานได้ แต่ Windows Application Control บล็อก DLL ตอน import Rasterio จึงอ่าน GeoTIFF ผ่าน Pillow/libtiff ไม่ได้เปลี่ยนนโยบายความปลอดภัยของเครื่อง
3. **ผ่าน:** เก็บ snapshot OSM และ Copernicus พร้อม URL/ใบอนุญาต/วันดาวน์โหลด/hash; แปลงพิกัดและตัด Tile
4. **ผ่าน:** มี terrain, roads, water และ building massing จากข้อมูลต้นทาง; ส่งออก GLB/BLEND แล้วตรวจเปิด BLEND และ import GLB ใหม่ด้วย Blender แยก process ทั้งคู่ผ่าน (610 meshes, UV/material/metadata ครบ, bounds error < 0.000001 ม.)

การตรวจซ้ำล่าสุดยืนยัน GIS tests 5/5, source receipts 6 รายการตรง SHA-256 และไฟล์ส่งออกครบ ผลลัพธ์ Tile มีอยู่ก่อนการตรวจรอบนี้ จึงไม่ได้สร้างทับ; ฉาก Blender ที่เปิดอยู่และโฟลเดอร์ Unity ที่ยัง untracked ถูกเก็บไว้ไม่แก้ไข ดู `reports/phase_report.json` สำหรับข้อจำกัดและหลักฐานล่าสุด

CLI `game-dev` ไม่พบใน PATH การตรวจ asset จึงใช้ตัวตรวจในโครงการนี้และ Blender โดยตรง

## รันซ้ำบน Windows PowerShell

เปิด PowerShell ที่โฟลเดอร์ `Ayutthaya3D`:

```powershell
# ใช้ environment ที่ติดตั้งแล้ว และ raw snapshot เดิม ไม่เรียกเครือข่าย
& '.\.venv\Scripts\python.exe' '.\scripts\run_pipeline.py' --offline --resume

# หากย้ายเครื่อง ให้ติดตั้ง environment ใหม่ก่อน
python -m venv .venv
& '.\.venv\Scripts\python.exe' -m pip install -r requirements-lock.txt

# ระบุ Blender เองได้
& '.\.venv\Scripts\python.exe' '.\scripts\run_pipeline.py' --offline --blender 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
```

Pipeline ใช้ MAX_WORKERS=1 และ Blender 2 threads พร้อม log/สถานะ/hash สำหรับ resume
ตรวจ hash input และ output ก่อนข้ามขั้นตอน; validation จะทำซ้ำเสมอ
เมื่อ rerun จะเขียนทับผลลัพธ์ใน Tile เดิม ต้องสำเนาโฟลเดอร์ก่อนสร้างรุ่นที่ต้องการเก็บแยก
การเรียก fetch ซ้ำจะใช้ snapshot เดิมที่ hash ตรงกัน ไม่ได้ดึง snapshot ใหม่มาทับต้นฉบับ
เป็น **sequential demo runner** ยังไม่ใช่ task queue แบบขนานเต็มรูปแบบของ Phase 8

```powershell
# ตัวสร้าง GIS รองรับ 250 / 500 / 1000 เมตร ภายใน source extent ที่มีข้อมูล
# คำสั่งนี้เขียนทับ recipe ของ Demo ควรใช้สำเนาโครงการเมื่อทดลองพื้นที่ใหม่
& '.\.venv\Scripts\python.exe' '.\scripts\gis\build_tile.py' --size 500 --origin 670100 1588100

# ทดสอบเฉพาะ GIS และตรวจไฟล์ GLB
& '.\.venv\Scripts\python.exe' '.\scripts\validators\test_gis.py' -v
& '.\.venv\Scripts\python.exe' '.\scripts\validators\validate_glb.py'
```

## โครงสร้างและหลักฐาน

- `data/raw/` — OSM response, Overpass query, GeoTIFF ต้นฉบับ และ XML ของ Copernicus
- `data/metadata/` — sources.json และสำเนาข้อกำหนดสิทธิ์ พร้อม receipt ของไฟล์ที่ดาวน์โหลด
- `data/processed/` — GeoJSON WGS84, JSON UTM พร้อม EPSG, DEM crop + georeferencing, mesh recipe
- `scripts/gis/` — download / reproject / clip / triangulate / estimate
- `scripts/blender/` — สร้าง Mesh, UV, materials, export และ render
- `scripts/validators/` — unit tests, GLB byte inspection และ Blender roundtrip
- `exports/unity/`, `exports/unreal/` — manifest และคำอธิบายแกน/หน่วย ยังไม่ใช่ engine scene
- `assets/` — โฟลเดอร์สำรองสำหรับ asset library ระยะต่อไป; demo geometry อยู่ใน recipe/Blend
- `logs/` — log และ checkpoint ของ runner
- `reports/` — GIS, Blender, GLB และ export roundtrip validation

การทดสอบผ่าน: roundtrip พิกัด, pixel-centre ของ GeoTIFF, polygon มีรู, seam ของ synthetic adjacent tiles,
การอ่านหน่วยวัด, ขนาด 500 ม., UV/material/provenance, geometry validity, index/accessor bounds,
source hashes, และการนำ GLB/BLEND กลับเข้า Blender

## สิ่งที่ยังอยู่ Phase ถัดไป

ยังไม่มี LOD, collision components ในเกม, scene streaming/Addressables/World Partition, ตัวละคร/รถ,
markings/curbs/intersection engineering, modular facades, vegetation, props, historical asset library หรือ FBX
ยังไม่ได้เปิดทดสอบ Unity หรือ Unreal และ **GLB ไฟล์เดียวไม่ได้ทำให้ World Streaming ทำงาน**
โค้ดรองรับ Tile size แต่ยังไม่ได้ขยายเป็นเกาะเมืองหรือทั้งจังหวัด

อ่าน `ATTRIBUTION.md` ก่อนนำโมเดลหรือข้อมูลไปเผยแพร่ แหล่งข้อมูลมีเงื่อนไขแยกกัน ไม่ใช่แพ็ก CC0 ทั้งชุด
