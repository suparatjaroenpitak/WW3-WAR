# Ayutthaya Open World — GIS-to-Game Pipeline

A source-traceable Blender/GIS project for building a real-coordinate, 1:1 metre-scale Ayutthaya world for Unity and Unreal. Processing is tile-based; no synthetic terrain or invented geographic features are substituted for missing source data.

## Phase 1 / Demo scope

This first checkpoint downloads actual Copernicus DEM GLO-30 and OpenStreetMap data for a **1 × 1 km tile centered on the geocoded Mueang Ayutthaya municipality reference point**. It samples the verified Copernicus COG into a UTM zone 47N (EPSG:32647) 30 m `terrain_30m.npy` raster using the configured reference point as local origin, and produces clipped OSM GeoJSON, and builds a source-attributed Blender scene with separate terrain, roads, waterways and building footprints. This is a GIS proof-of-pipeline, not a full-city reconstruction or a finished game world.

**Important resolution limitation:** a 1 km area sampled at 30 m is roughly 34 × 34 terrain vertices. GLO-30 is a DSM (surface model), not a bare-earth LiDAR DTM. It cannot accurately represent walkable microtopography, individual small roads, or vehicle-ready terrain. The tile is a pipeline demonstration; use authoritative higher-resolution elevation and road elevation/bridge data for production gameplay.

## Phases

1. **Project Setup and GIS Pipeline** — implemented by this demo.
2. Terrain generation — DEM sampling, no-data checks, metric local origin.
3. Roads, rivers and bridges — sourced OSM features, classified, independently meshed.
4. Procedural buildings — only footprints with OSM building tags; procedural height is not geographic survey data and must be marked as inferred.
5. Temples / historic landmarks — individually sourced/licensed survey or reference assets only; no invented reconstruction.
6. Vegetation / environment — open/licensed sources with provenance.
7. PBR materials / UVs — separate material library and documented texture licenses.
8. Optimization / LOD — tile-local collision and LOD, source meshes preserved.
9. GLB/FBX export — separate exports and manifests; validate scale and transforms.
10. Unity/Unreal integration — import, origin alignment, streaming and traversal validation.

## Data sources and attribution

- **Reference origin:** Nominatim result for Mueang Ayutthaya municipality, OSM relation `10839539`, retrieved 2026-10-12. Source query URL is recorded in `config/project.json` and every generated tile manifest. It is a local demo origin, **not** the province boundary or a claim that this 1 km tile covers the municipality.
- **Elevation:** Copernicus DEM GLO-30 Public, DSM, COG 1° tile `N14_00_E100_00`; provider docs and license URLs are in the project config and per-tile metadata. Required notice: `produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved`.
- **Roads, water, buildings:** OpenStreetMap contributors, downloaded live from the configured Overpass API. ODbL 1.0. Attribute `© OpenStreetMap contributors` and link https://www.openstreetmap.org/copyright. ODbL share-alike obligations apply to a derivative database; review the license before distributing combined derived data.
- **Boundary data:** no provincial boundary dataset is bundled or inferred. GADM is deliberately not used: its published terms prohibit commercial use/redistribution without permission, unsuitable for a distributable game pipeline.
- **Imagery:** none is downloaded or used in this demo.

Every fetch writes `data/<tile>/sources.json` with exact requested URL, provider/license/attribution, UTC retrieval time and SHA-256. Keep it alongside derivative exports.

## Setup

Requirements: Python 3.10+, Blender 4.x/5.x with GLB and FBX exporters, network access to the Copernicus public S3 bucket and Overpass. On Windows with the inspected Blender install:

```bash
cd AyutthayaOpenWorld
python -m venv .venv
.venv/Scripts/python -m pip install -r requirements.txt
.venv/Scripts/python scripts/download_tile.py --config config/project.json
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python blender/build_demo.py -- --config config/project.json
```

For a running Blender, `blender/build_demo.py` is also designed to run via Blender MCP `execute_blender_code` (load the script in Blender's Python console or send its source through the MCP tool). Always execute the downloader first. Blender's Python has its own environment; do not assume GIS dependencies are installed into it—the build script reads generated GeoTIFF/GeoJSON using Blender's built-in Python and `bpy`.

Default outputs:

- `data/AYU_UTM47N_E000_N000/terrain_30m.npy` (30 m float32 samples; source GeoTIFF retained beside it)
- `data/AYU_UTM47N_E000_N000/osm.geojson`
- `data/AYU_UTM47N_E000_N000/sources.json`
- `output/AYU_UTM47N_E000_N000/demo.blend`
- `output/AYU_UTM47N_E000_N000/demo.glb`
- `output/AYU_UTM47N_E000_N000/demo.fbx`
- `output/AYU_UTM47N_E000_N000/collision_terrain.fbx` (separate collision mesh)
- `output/AYU_UTM47N_E000_N000/sources.json` (copied provenance/license manifest)

Generated source data and deliverables are excluded from version control; retain the per-tile provenance manifest when packaging them.

## Phase 1 validation

```bash
python -m unittest discover -s tests -v
python scripts/download_tile.py --config config/project.json
# Then run Blender command above; its script validates tile dimensions and reports exports.
```

The data downloader respects Overpass's public service; do not run concurrent/bulk requests. For larger regions use a licensed regional extract/provider and rate-limited chunk jobs. Do not start full-province downloading until a licensed provincial AOI is selected and storage/runtime budget is agreed.

## Scale and coordinate contract

All Blender geometry is in metres. The origin is configured longitude/latitude projected into EPSG:32647 and set to local `(0, 0, 0)`; tile coordinates are offsets in that projected CRS. Elevation is relative to DEM values around the local origin, not an absolute global elevation datum. Export units remain metres; do not apply arbitrary scale in Unity or Unreal. A province-sized world requires additional streaming, georeferencing and floating-origin design beyond this demo.
