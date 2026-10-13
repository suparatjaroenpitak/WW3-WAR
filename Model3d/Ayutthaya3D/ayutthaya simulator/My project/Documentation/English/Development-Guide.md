# AYUTTHAYA SIMULATOR — development guide

This is a playable foundation, not a finished province-scale game. Do not treat unverified systems as complete.

## 1. What is installed

- Unity project: `Model3d/Ayutthaya3D/ayutthaya simulator/My project`
- Editor version in `ProjectSettings/ProjectVersion.txt`: **6000.6.5f1**
- Active quality pipeline: URP asset `Assets/Settings/PC_RPAsset.asset`
- Input: Input System package only (`activeInputHandler: 1`). Do not use the legacy Input Manager.
- The WW3 WAR project in the parent folder was not modified.

## 2. Open the demo

1. Open the Unity project above in Unity 6000.6.5f1.
2. Open `Assets/_AyutthayaSimulator/Scenes/AyutthayaDemo.unity`.
3. Press Play. `DemoSceneBootstrap` builds the world if `WorldRoot` is missing.
4. Optional editor bake: **Tools > Ayutthaya Simulator > Generate Demo Scene**.

If the Console shows compiler errors from `com.unity.dt.app-ui`, that package belongs to the template (`com.unity.ai.inference`), not to this simulator. Play Mode stays blocked until that package compiles. Do not claim the demo was play-tested while those errors remain.

## 3. Controls

| Key | Action |
| --- | --- |
| WASD | Move on foot, or steer/accelerate in a vehicle |
| Mouse | Look |
| Shift | Run |
| C or Ctrl | Crouch |
| Space | Jump, or brake while driving |
| E | Interact, open a door, talk, enter vehicle |
| F | Exit vehicle |
| Esc | Pause |
| F5 / F9 | Save / load JSON |
| L | Thai / English |
| R | Clear / rain |
| P | Screenshot request. Capture itself is not verified in this session |

## 4. What the demo tile actually contains

- Center: Wat Phra Si Sanphet, from Nominatim/OSM, id `37447698`, 14.3559133 N, 100.5585079 E.
- Projection: EPSG:4326 to EPSG:32647. Game offset adds 250 m so tile 0 covers 0..500 m.
- 132 building footprints, 81 road pieces, 8 water features from the OSM map API.
- License: Open Database License 1.0. Attribution must stay visible: © OpenStreetMap contributors.
- Metadata: `Model3d/Ayutthaya3D/data/metadata/demo_tile_500m.metadata.json`.
- Terrain is a **flat placeholder**. No DEM was downloaded. It is not surveyed elevation.
- Building heights are estimated unless `heightSource` is `osm_height_tag`.
- Road widths are estimated unless `widthSource` is `osm_width_tag`.
- Doors, the red vehicle, the guide NPC and street lights are procedural. They are not surveyed objects.
- Historic tags are metadata only. No temple reconstruction is claimed.

## 5. Script binding

A file or object name is never enough.

1. Select the imported model root.
2. Run **Tools > Ayutthaya Simulator > Validate Asset**.
3. If the report has no mesh, stop. Do not add gameplay scripts.
4. Set a `ModelImportProfile` manual override when the kind is known.
5. Run **Auto Bind Components** only after validation.
6. **Generate Colliders** adds a non-convex MeshCollider and marks that object static.
7. **Setup LOD** uses existing renderers. It does not invent a second mesh from a name.
8. **Generate Prefab** refuses to save when validation failed.

Expected hierarchy for a building prefab:

```
BuildingRoot
  LOD0            MeshFilter + MeshRenderer + MeshCollider
  LOD1            optional simpler renderer
  Door            separate mesh, DoorController, PivotVerified unchecked until reviewed
```

## 6. Save, missions, streaming

- Save file: `Application.persistentDataPath/ayutthaya-simulator-save.json`, version 1.
- Loading restores position, language, time, weather and mission ids. It does not put the player back inside a vehicle.
- Missions: walk to the yellow marker, talk to `guide_npc`, enter `demo_vehicle`.
- Adjacent tiles are not generated. `WorldStreamer` can mark a missing tile unavailable. It must not invent geometry.
- One GLB/scene file is not a streaming world.

## 7. Not done

- Play Mode, driving, NPC navigation and FPS were not measured.
- Windows player build was not run.
- Blender MCP was not connected, so no BLEND/GLB export is claimed.
- Addressables and Unity Localization packages are not installed. Thai/English uses `LocalizationCatalog`.
- No interior meshes, no surveyed historical reconstruction, no province-wide world.
