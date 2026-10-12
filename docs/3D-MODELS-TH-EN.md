# WW3 War — คู่มือใช้โมเดล 3D ที่สร้างขึ้น / Generated 3D Model Manual

[หน้าแรก / README](../README.md) · [คู่มือพัฒนาไทย](DEVELOPMENT.th.md) · [English development guide](DEVELOPMENT.en.md)

> **TH — อ่านก่อน:** โมเดลที่มีอยู่ตอนนี้เป็น **procedural models ที่สร้างจาก Unity primitives ด้วย C# ขณะรัน** ไม่ใช่ไฟล์ FBX/OBJ/GLB ที่ดาวน์โหลดจาก AI และไม่ใช่โมเดลจาก ROME II คู่มือนี้อธิบายสิ่งที่มีจริง พร้อมแยกตัวอย่างการดัดแปลงออกจากฟีเจอร์ที่ติดตั้งแล้ว
>
> **EN — Read first:** The current models are **runtime C# procedural assemblies of Unity primitives**, not downloaded AI-generated FBX/OBJ/GLB assets or ROME II models. This manual distinguishes current behavior from optional modifications.
>
> **Verification / การตรวจ:** เนื้อหาเทียบกับ source ปัจจุบันแล้ว แต่ตัวอย่าง Unity ในเอกสารยังไม่ได้ compile หรือทดลองใน Editor เพราะยังไม่มี Editor ที่ยืนยันพร้อมใช้ในรอบงานนี้ / The instructions were checked against the current sources. Unity examples have not been compiled or exercised in an Editor; they are not evidence of a working imported-model pipeline. See [verification record](VERIFICATION.md).

## สารบัญ / Contents

1. [โมเดลอยู่ที่ไหน / Asset location](#model-location)
2. [เปิดดูในเกม / Viewing in the game](#view-models)
3. [โครงสร้างและวงจรชีวิต / Hierarchy and lifecycle](#model-lifecycle)
4. [แกน ขนาด และ pivot / Coordinates and scale](#coordinates)
5. [รายละเอียดทหารราบ / Infantry geometry](#infantry)
6. [รายละเอียดรถและปืนใหญ่ / Vehicle geometry](#vehicles)
7. [ฉาก ที่กำบัง และวงเลือก / Scenery, cover and rings](#terrain)
8. [API สร้างโมเดล / Factory API](#factory-api)
9. [สี วัสดุ และ shader / Materials and shader](#materials)
10. [ปรับโมเดลอย่างปลอดภัย / Safe customization](#customization)
11. [แยกโมเดลออกจากกฎเกม / Visuals versus gameplay](#gameplay-boundary)
12. [โมเดล 3D ที่ gen จากภายนอก / Externally generated assets](#external-models)
13. [ตัวอย่างแทนที่ด้วย prefab / Optional prefab integration](#prefab-integration)
14. [เก็บเป็น asset และ export / Persistence and export](#persistence)
15. [Animation และจุดยิง / Animation and muzzle points](#animation)
16. [ประสิทธิภาพ / Performance](#performance)
17. [แก้ปัญหา / Troubleshooting](#troubleshooting)
18. [Checklist ส่งมอบ / Acceptance checklist](#acceptance)

<a id="model-location"></a>
## 1. โมเดลอยู่ที่ไหน / Asset location

| ไฟล์จริง / Existing file | ใช้ทำอะไร / Purpose |
| --- | --- |
| [ProceduralFactory.cs](../Assets/WW3War/Scripts/Runtime/ProceduralFactory.cs) | `UnitModel` สร้างทหาร/รถ; `Terrain` สร้างฉาก; `Shape`, `Box`, `Ring`, `Material` เป็นตัวช่วย / Constructs unit geometry, scenery, shapes, rings and materials |
| [WW3WarGame.cs](../Assets/WW3War/Scripts/Runtime/WW3WarGame.cs) | `BeginBattle`, `SpawnSide`, `TickBattle`, `DrawTracer`, `FinishBattle` เชื่อมภาพกับการรบ / Connects model creation, movement, shots and cleanup to battle state |
| [UnitCatalog.cs](../Assets/WW3War/Scripts/Core/UnitCatalog.cs) | HP ความเร็ว ระยะยิง damage ราคา upkeep; ไม่ใช่ geometry / Gameplay statistics, not geometry |
| [CampaignModels.cs](../Assets/WW3War/Scripts/Core/CampaignModels.cs) | `UnitKind.Infantry`, `Armor`, `Artillery` และข้อมูล formation / Unit kinds and formation data |
| [FieldSurface.shader](../Assets/WW3War/Resources/FieldSurface.shader) | Shader ที่ factory โหลดด้วยชื่อ Resources `FieldSurface` / Resource-loaded color shader |
| [RTSCamera.cs](../Assets/WW3War/Scripts/Runtime/RTSCamera.cs) | มุมกล้องสำหรับตรวจภาพ / Tactical camera |

**TH:** ไม่มีไฟล์โมเดลแยกให้ลากลงฉากในระบบปัจจุบัน รูปทรงเกิดจาก `GameObject.CreatePrimitive` แล้วประกอบเป็นลูกของ root หน่วย ชื่อใน Hierarchy เช่น `Blue P1` ไม่ใช่ prefab asset ที่บันทึกไว้บนดิสก์

**EN:** There is no standalone model file to drag into the scene in the current system. `GameObject.CreatePrimitive` builds the geometry under each unit root. Hierarchy entries such as `Blue P1` are runtime objects, not saved prefab assets.

<a id="view-models"></a>
## 2. เปิดดูในเกมทีละขั้น / Viewing in the game

### ภาษาไทย

1. เปิดโปรเจกต์ด้วย Unity **6000.0.84f1** แล้วเลือก **WW3 War → Open Main Scene**
2. กด Play จากนั้นเลือก **Kestrel Ridge** ซึ่งเชื่อมกับ Aster Harbor แล้วกด **Launch Operation**
3. จะสร้าง root ชื่อ `Procedural Modern Battlefield` ใน Hierarchy ภายในมีพื้นถนน ฉาก และ root ของหน่วย เช่น `Blue P1`, `Blue P2`, `Blue P3`, `Red ...`
4. กด Pause ของเกมก่อนดูรายละเอียด เพื่อลดการเปลี่ยนตำแหน่งของหน่วย ปุ่ม Pause ของเกมไม่ใช่ปุ่ม Pause ของ Unity Editor
5. เปิด Scene view เลือก root ของหน่วยใน Hierarchy แล้วกด **F ใน Scene view** เพื่อ frame object; ขยายลูกเพื่อเลือกหมวก ป้อมปืน หรือปืนหลัก กด **F ใน Game view** กลับเป็นคำสั่ง hold ของเกม ต้องระวัง focus
6. ใน Game view ใช้ WASD, Q/E และ wheel ดูจากหลายมุม คลิกการ์ดด้านล่างหรือคลิกหน่วยสีน้ำเงินเพื่อเห็นวงเลือก
7. แก้ Transform ใน Inspector ระหว่าง Play ได้เพื่อทดลอง แต่เมื่อหยุด Play การแก้ runtime objects จะหาย ต้องแก้ source หรือสร้าง asset ถาวรตามหัวข้อ 14

### English

1. Open with Unity **6000.0.84f1**, choose **WW3 War → Open Main Scene**, and enter Play Mode.
2. Select **Kestrel Ridge**, adjacent to Aster Harbor, then launch the operation.
3. Expand `Procedural Modern Battlefield` in the Hierarchy. Unit roots include `Blue P1`, `Blue P2`, `Blue P3` and red enemy roots.
4. Pause the in-game simulation while inspecting; the game's pause differs from the Editor pause control.
5. In **Scene view**, select a unit and press **F** to frame it. In **Game view**, F means hold position. Expand the hierarchy to inspect individual parts.
6. Use WASD/Q/E/wheel in Game view to inspect angles and the roster/world selection to show a selection ring.
7. Inspector edits to runtime objects are temporary. Save intentional changes in code or authored assets rather than assuming they survive leaving Play Mode.

<a id="model-lifecycle"></a>
## 3. โครงสร้างและวงจรชีวิต / Hierarchy and lifecycle

โครงสร้างย่อของหน่วยรถหนึ่งคัน / Simplified current vehicle hierarchy:

```text
Procedural Modern Battlefield
├── Battlefield, Highway, Access road, ...
├── Tactical ring                 ← relay decoration, radius 8
├── Tactical ring                 ← reusable selection marker, radius 3.5
├── Blue P2                       ← gameplay root: position/rotation
│   ├── Ground shadow
│   ├── Track left
│   ├── Track right
│   ├── Armored chassis
│   ├── Turret                    ← scaled cube, not an unscaled pivot
│   │   └── Main gun              ← inherits turret scale/rotation
│   ├── Identification panel
│   └── Antenna
└── Red ...
```

**TH:** `UnitModel` ไม่ได้สร้าง root และไม่คืนค่า มันเพิ่มชิ้นส่วนลงใน `parent` ที่ส่งเข้าไป ส่วน `SpawnSide` สร้าง root, วางตำแหน่ง, หมุน, เรียก factory แล้วเก็บ reference ไว้ใน `BattleUnit.Root` การเรียก `UnitModel` ซ้ำบน parent เดิมจะเพิ่มชิ้นส่วนซ้ำ ไม่ได้ replace ของเก่าให้

**EN:** `UnitModel` returns `void` and does not create the gameplay root. It appends geometry to the supplied parent. `SpawnSide` owns the root and stores it in `BattleUnit.Root`. Calling `UnitModel` twice on the same parent duplicates geometry instead of replacing it.

Lifecycle / ลำดับ:

```text
BeginBattle → Terrain + selection ring → SpawnSide → UnitModel
                                      ↓
                      TickBattle moves/rotates unit roots
                                      ↓
          unit death: Destroy(unit.Root); remove from battle list
                                      ↓
                   FinishBattle: Destroy(battlefield)
```

รากสนามรบถูกลบเมื่อจบ operation วัสดุเก็บใน static cache แยกจาก root จึงไม่ควรสั่ง `Destroy` shared material เมื่อหน่วยตาย / The battlefield root is destroyed at the end of an operation. Materials are held in a separate static cache; do not destroy a shared material when an individual unit dies.

<a id="coordinates"></a>
## 4. แกน ขนาด และ pivot / Coordinates and scale

- **X:** ซ้าย/ขวา; **Y:** สูงขึ้น; **Z+:** ด้านหน้าของโมเดล / X is lateral, Y is up, and +Z is model forward.
- root gameplay อยู่บนระนาบ `y = 0`, scale เริ่ม `(1,1,1)` / Gameplay roots begin on y=0 with unit scale.
- `Shape` ใช้ `SetParent(parent, false)` แล้วกำหนด `localPosition`/`localScale` ไม่ใช่ world coordinates / Factory positions and scales are local to the supplied parent.
- กลุ่มทหารหกคนถือเป็น **หนึ่ง formation**; ไม่ใช่หกหน่วยที่มี HP และคำสั่งแยก / Six visible soldiers represent one gameplay formation.
- ต้นแบบใช้ world units แบบอาร์เคด ไม่ยืนยัน 1 unit เท่ากับ 1 เมตรจริง แม้ข้อความ UI บางจุดใช้ `m` / Treat world units as arcade units, not verified physical meters.

| Primitive | รูปทรงพื้นฐานก่อน scale / Base dimensions before scaling |
| --- | --- |
| Cube | 1 × 1 × 1 |
| Sphere | Diameter 1 |
| Capsule | Height 2, diameter 1 |
| Cylinder | Height 2, diameter 1 |

**TH:** ดังนั้น Capsule `scale.y = .66` สูงประมาณ 1.32 units ไม่ใช่ .66; Cylinder `scale.y = .015` หนาประมาณ .03 ขนาดนี้ยังต้องคูณ parent scale ทุกชั้น ห้ามตีความตัวเลข `localScale` เป็น world dimensions โดยไม่ตรวจชนิด primitive และ hierarchy

**EN:** A capsule scaled to y=.66 is approximately 1.32 units tall, and a cylinder scaled to y=.015 is approximately .03 thick. Parent scales apply as well. `localScale` is not a world-size declaration.

ใน `SpawnSide` ปัจจุบันฝ่ายเราเริ่มแถว z=-34 หมุน y=0; ศัตรูเริ่ม z=34 หมุน y=180 และมีการสลับ offset ±5 ตามลำดับหน่วย ระยะ x ระหว่าง formation คือ 9 units โมเดลควรอยู่รอบ origin ไม่เยื้องไปไกล ไม่เช่นนั้นระยะยิงและจุดคลิกจะไม่ตรงภาพ / Player units start around z=-34 facing +Z; enemies start around z=34 facing -Z, with alternating offsets. Formation spacing is 9 units. Keep visual geometry centered around the gameplay origin.

<a id="infantry"></a>
## 5. รายละเอียดทหารราบ / Infantry geometry

`UnitModel(parent, UnitKind.Infantry, team)` สร้าง ground-shadow 1 ชิ้น และทหาร 6 คน คนละ 4 ชิ้น รวม **25 primitive renderers** ไม่รวม gameplay root / One shadow plus six soldiers with four parts each: **25 primitive renderers**, excluding the gameplay root.

แต่ละคนมีฐานตำแหน่ง / Each soldier's base position:

```csharp
var p = new Vector3((i % 3 - 1) * 1.35f, 0, (i / 3 - .5f) * 1.7f);
// i = 0..5, integer division i / 3 → two rows of three
```

| ชิ้นส่วน / Part | Primitive | local position relative to `p` | local scale |
| --- | --- | --- | --- |
| Infantry torso | Capsule | `(0, .95, 0)` | `(.48, .66, .48)` |
| Helmet | Sphere | `(0, 1.68, 0)` | `(.51, .51, .51)` |
| Rifle | Cube | `(.28, 1.1, .49)` | `(.12, .13, 1.0)` |
| Pack | Cube | `(0, 1.03, -.31)` | `(.46, .6, .24)` |
| Ground shadow (one per formation) | Cylinder | `(0, .025, 0)` from root | `(4.3, .015, 4.3)` |

**TH:** ทั้ง 6 คนไม่มี parent แยกรายคนและไม่มีข้อต่อ ทุก `Infantry torso`, `Helmet`, `Rifle`, `Pack` เป็นลูกตรงของ root และชื่อซ้ำกัน จึงไม่ควรใช้ `Find("Helmet")` แล้วคาดว่าจะได้หมวกทั้งหมด ไม่มีเดิน ยิง หรือ ragdoll animation และจำนวนคนที่มองเห็นไม่ลดลงตาม HP; หน่วยถูกลบทั้ง formation เมื่อ HP หมด

**EN:** Soldiers have no individual sub-roots or rigs; repeated part names are direct children of the formation root. `Find("Helmet")` is not an all-helmets query. There are no walk/fire/ragdoll animations, and individual soldiers do not disappear proportionally to health.

การปรับระยะห่างแก้ตัวคูณ 1.35 และ 1.7; ถ้าเพิ่มจำนวนทหารต้องปรับสูตรจัดแถว เงา วงเลือก และงบ renderers พร้อมกัน ไม่ต้องเปลี่ยน HP เว้นแต่ตั้งใจปรับ balance / Change 1.35/1.7 for spacing. Increasing visible soldiers requires revisiting layout, shadow, selection radius and renderer cost; it does not automatically change HP.

<a id="vehicles"></a>
## 6. รายละเอียดรถและปืนใหญ่ / Armor and artillery geometry

`Armor` และ `Artillery` ใช้โครงเดียวกัน รวม **8 primitive renderers** ต่อ formation ปืนใหญ่ต่างตรงมุม Main gun `Quaternion.Euler(-16,0,0)` / Both use **8 primitive renderers**. Artillery tilts the main gun by -16 degrees around local X.

| Part | local position | local scale | Parent |
| --- | --- | --- | --- |
| Ground shadow | `(0,.025,0)` | `(4.3,.015,4.3)` | unit root |
| Track left | `(-1.15,.5,0)` | `(.62,.85,3.8)` | unit root |
| Track right | `(1.15,.5,0)` | `(.62,.85,3.8)` | unit root |
| Armored chassis | `(0,.92,0)` | `(2.35,.75,3.65)` | unit root |
| Turret | `(0,1.56,-.15)` | `(1.55,.75,1.75)` | unit root |
| Main gun | `(0,.04,1.15)` | `(.15,.18,1.7)` | **Turret** |
| Identification panel | `(0,1.96,-.2)` | `(.8,.08,.6)` | unit root |
| Antenna | `(.7,2.08,-1.1)` | `(.035,1.8,.035)` | unit root |

**จุดสำคัญ / Important:** ปืนเป็นลูกของ cube ป้อมที่ scale แล้ว ขนาด/ตำแหน่งปืนจึงถูกคูณ scale ของ Turret อีกครั้ง เช่น Main gun ของ Armor ที่ไม่มี rotation ยาวตาม Z = `1.7 × 1.75 = 2.975` units ก่อนคูณ root ส่วน Artillery มี rotation ทำให้แกนผสมกัน ห้ามแก้ป้อมแล้วคิดว่าปืนจะขนาดเดิม / The gun inherits turret scale. Armor's unrotated barrel length along Z is 2.975 units before root scaling; artillery rotation mixes axes.

ถ้าต้องการหมุนป้อมอย่างถูกโครงสร้าง ให้ปรับเป็น **unscaled pivot → visual mesh + gun** ในงานต่อไป ไม่ใช่เพิ่มโค้ดหมุน cube ที่ scale ต่างแกนแล้วโดยไม่ตรวจ distortion / For articulated turrets, introduce an unscaled pivot rather than relying on a nonuniformly scaled cube as an articulation root. This refactor is not implemented here.

<a id="terrain"></a>
## 7. ฉาก ที่กำบัง และวงเลือก / Scenery, cover and rings

`Terrain(root, covers)` สร้าง **GameObject primitives** ไม่ใช่ Unity Terrain component และเพิ่มจุดกำบังลง list ที่ส่งมา / This creates primitive geometry, not a Unity Terrain component, and appends cover points to the supplied list.

| สิ่งที่สร้าง / Element | รายละเอียดปัจจุบัน / Current detail |
| --- | --- |
| Battlefield | Cube scale `(154,.5,114)`, center `(0,-.3,0)` |
| Highway | `(148,.06,9)` at `(0,.006,0)` |
| Access road | `(6,.065,105)` at `(9,.008,0)` |
| Road marking | 29 cubes |
| Map grid | 15 X-lines + 11 Z-lines |
| Sandbag cover | 6 locations × 5 cubes = 30 cubes |
| Warehouses and roofs | 20 warehouse cubes + 20 roofs; local `System.Random(41)` |
| Relay | Cylinder platform, mast, 3 antennas, decorative 32-segment ring |
| Selection ring | Another 32-segment ring, built separately in `BeginBattle`, reused for the selected unit |

Cover locations / จุดกำบัง:

```text
(-22,0,-16), (-18,0,18), (22,0,-18), (20,0,18), (0,0,9), (0,0,-10)
```

**TH:** runtime ตรวจระยะจาก **root เป้าหมาย** ถึงจุดเหล่านี้ `< 5` world units แล้วลด damage ไม่ใช้ collider ของ sandbag และไม่ตรวจว่ามีกระสอบทรายคั่นแนวยิงจริง ถ้าย้ายภาพกระสอบแต่ไม่ย้ายจุดใน `covers` ผล gameplay จะไม่ตรงภาพ ถ้าย้าย/หมุน/scale root สนาม ควรแปลงจุดเหล่านี้เป็น world coordinates ด้วย `root.TransformPoint` ใน integration ที่แก้เอง ปัจจุบัน root สนามอยู่ origin และไม่มี scale

**EN:** Cover checks target-root distance `< 5` to the recorded points, not physical barriers or line-of-fire occlusion. Move the cover data together with its visuals. If you transform the battlefield root, convert local cover points into world space in your integration; the current battlefield root is identity-transformed.

Relay เป็นฉากประกอบ ไม่ใช่จุดยึดครอง โกดังไม่ขวางทางเดิน และผังโกดังไม่ได้เปลี่ยนตาม battle seed เพราะใช้ seed 41 คงที่ / The relay is decorative, warehouses do not block movement, and warehouse layout uses a fixed seed rather than the battle seed.

<a id="factory-api"></a>
## 8. API สร้างโมเดล / Factory API

ลายเซ็นต่อไปนี้ตรงกับ source / Current signatures:

```csharp
Material Material(Color color);
GameObject Shape(Transform parent, string name, PrimitiveType type,
    Vector3 pos, Vector3 scale, Color color);
GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Color color);
GameObject Ring(Transform parent, Color color, float radius, float height = .06f);
void UnitModel(Transform parent, UnitKind kind, int team);
void Terrain(Transform root, List<Vector3> covers);
```

ทั้งหมดเป็น `public static` ของ `WW3War.ProceduralFactory` / All are public static methods of `WW3War.ProceduralFactory`.

- `Shape`/`Box`: คืน GameObject ของชิ้นส่วนนั้น; collider ถูก disable แล้วสั่ง `Object.Destroy` / Return the shape object, with its primitive collider disabled and scheduled for destruction.
- `Ring`: คืน root ของวงที่มี 32 ลูก / Returns a root containing 32 arc cubes.
- `UnitModel`: ใช้ `UnitKind` ที่ถูกต้องเท่านั้น; factory เองไม่มี validation ของค่า enum / Supply a valid enum; the factory itself does not reject unknown enum values.
- `team`: ใช้ **1 สำหรับเรา, 2 สำหรับศัตรู**; factory แสดงทุกค่าที่ไม่ใช่ 1 เป็นสีฝ่ายศัตรู / Use 1 for player and 2 for enemy; the visual factory treats any non-1 value as the enemy palette.
- `Terrain`: ส่ง list ที่ไม่เป็น null และล้างก่อนใช้ซ้ำ; method append ไม่ได้ clear / Supply a non-null list and clear it before reuse; the method appends.

### ตัวอย่างสร้างภาพหนึ่ง formation / Example: creating one visual formation

ตัวอย่างนี้เป็น helper ที่จะเพิ่มเองใน C# ไม่ใช่ script ที่ติดตั้งอยู่แล้ว ใช้เฉพาะระหว่าง runtime หลังมี parent และเรียกจาก code ที่ควบคุมอยู่ การสร้างภาพอย่างเดียว **ไม่เพิ่มหน่วยใน battleUnits** / This optional helper creates only visuals. It does **not** register a playable battle unit.

```csharp
using UnityEngine;
using WW3War;
using WW3War.Core;

public static class ModelUsageExample
{
    public static GameObject CreateVisual(Transform battlefield, UnitKind kind, int team)
    {
        var root = new GameObject("Example visual only");
        root.transform.SetParent(battlefield, false);
        root.transform.localPosition = new Vector3(0, 0, -20);
        root.transform.localRotation = Quaternion.Euler(0, team == 1 ? 0 : 180, 0);
        root.transform.localScale = Vector3.one;
        ProceduralFactory.UnitModel(root.transform, kind, team);
        return root;
    }
}
```

**TH:** ไม่ควรติดสคริปต์สร้างซ้ำทุก `Update`/`OnGUI` เพราะจะสร้างชิ้นส่วนทับทุกเฟรม ใช้ครั้งเดียวตอน spawn และลบ root เมื่อเลิกใช้ อย่าเรียก factory จาก Edit Mode ตรง ๆ เพราะมี `Object.Destroy` ที่ออกแบบสำหรับ runtime

**EN:** Call once on spawn, not in Update or OnGUI, and destroy the root when done. Do not treat this runtime factory as an Edit Mode authoring utility; it uses runtime `Object.Destroy` semantics.

<a id="materials"></a>
## 9. สี วัสดุ และ shader / Materials and shader

### สีปัจจุบัน / Current palette (RGB 0–1)

| Usage | Color |
| --- | --- |
| Blue team trim | `(.23,.66,.76)` |
| Red team trim | `(.83,.35,.25)` |
| Player body | `(.29,.43,.40)` |
| Enemy body | `(.53,.40,.29)` |
| Dark parts | `(.12,.16,.16)` |
| Ground shadow | `(.21,.24,.20)` |
| Sand | `(.53,.52,.40)` |

Turret ใช้ `bodyColor * .86f` ส่วน panel/helmet ใช้ trim / Turrets use body color multiplied by .86; panels and helmets use team trim.

### Material cache

**TH:** factory cache `Dictionary<Color, Material>` และกำหนด `Renderer.sharedMaterial` ชิ้นสีเดียวกันจึงใช้ Material reference เดียวกัน เปลี่ยน `sharedMaterial.color` ของชิ้นเดียวจะกระทบทุกชิ้นที่อ้าง material นั้น และ cache key สีเดิมอาจไม่ตรงสีใหม่อีก จึงควรเปลี่ยนสีต้นทางแล้วสร้างใหม่ หรือ assign `ProceduralFactory.Material(newColor)` ให้ชิ้นที่ต้องการ อย่าเปลี่ยนสีกลางของ cache แบบเงียบ ๆ

**EN:** Materials are cached by Color and assigned through `sharedMaterial`. Mutating a cached material affects all users and can make the cache's key disagree with its current color. Prefer changing the palette before generation or assigning another cached material to the intended renderer.

ตัวอย่างเปลี่ยนเฉพาะชิ้นภาพที่เลือก / Optional targeted reassignment:

```csharp
// part is the specific GameObject you already selected or retained.
var renderer = part.GetComponent<Renderer>();
if (renderer != null)
    renderer.sharedMaterial = ProceduralFactory.Material(new Color(.2f, .5f, .3f));
```

หลีกเลี่ยงสุ่มสีใหม่ทุกเฟรม: ทุก Color ที่แตกต่างอาจสร้าง Material เพิ่มและ cache ไม่ได้ล้างต่อ operation การเรียก `renderer.material` อาจสร้าง material instance ใหม่ซึ่งต้องมี owner จัดการ cleanup / Avoid generating distinct colors every frame; the cache persists across operations. Accessing `renderer.material` may create an instance requiring explicit ownership and cleanup.

### Shader จริง / Actual shader behavior

1. โหลด `Resources.Load<Shader>("FieldSurface")`; ถ้าไม่เจอ fallback `Shader.Find("Standard")` / Loads FieldSurface, then falls back to Standard if missing.
2. FieldSurface รับ `_Color` และคำนวณแสงจาก normal กับทิศคงที่ใน shader / It uses `_Color` and a hard-coded normal/light direction.
3. ไม่มี texture/normal/metallic maps, transparency blend, animated material หรือ shadow-caster pass / No texture slots, PBR maps, transparency blending, material animation or shadow-caster pass are implemented.
4. การหมุน Directional Light จึงไม่ปรับทิศแสงของ FieldSurface; วงกลมมืดใต้หน่วยเป็น geometry ไม่ใช่เงาจริง / Rotating the scene light does not change this shader's baked direction; the ground shadow is geometry, not a real-time shadow.
5. เป้าหมายเป็น **Built-in pipeline**; อย่าคาดว่า shader นี้ใช้กับ URP/HDRP ได้ทันที / URP/HDRP compatibility is not established.

<a id="customization"></a>
## 10. ปรับโมเดลอย่างปลอดภัย / Safe customization

### A. เปลี่ยนสี / Recolor

แก้ body/trim ใน `UnitModel` และค่าคงที่ Blue/Red/Sand ใน factory แล้วหยุด/เริ่ม Play หรือเปิด operation ใหม่ ระวัง static cache เมื่อปิด domain reload / Change the factory palette and regenerate the visuals. Be mindful of static cache persistence when domain reload is disabled.

### B. เพิ่มความยาวปืน / Change gun length

แก้ scale ของ `Main gun` แล้วตรวจผลของ parent Turret ตามหัวข้อ 6 อย่าเพิ่ม `UnitCatalog.Range` ถ้าแค่เปลี่ยนความยาวภาพ / Edit barrel geometry and account for inherited scale. Visual barrel length does not automatically change weapon range.

### C. เปลี่ยนขนาดภาพทั้งหน่วย / Resize only the visual

ไม่แนะนำ scale gameplay root เพราะอาจทำให้จุดอ้างอิงภาพ/คำสั่งสับสน ใช้ child ใหม่ชื่อ `Visual` ที่ identity transform แล้วเรียก factory บน child นั้นแทน ใน `SpawnSide` ให้ **แทนที่** บรรทัด `ProceduralFactory.UnitModel(root.transform, roster[i].Kind, team);` ไม่ใช่เรียกเพิ่ม / Replace the existing UnitModel call in SpawnSide with an optional visual child:

```csharp
var visual = new GameObject("Visual");
visual.transform.SetParent(root.transform, false);
visual.transform.localScale = Vector3.one * 1.2f;
ProceduralFactory.UnitModel(visual.transform, roster[i].Kind, team);
```

root ยังใช้ตำแหน่งเดิมและ `BattleUnit.Root` เหมือนเดิม แต่วงเลือกและ click radius ไม่ขยายตามโมเดล จึงต้องตรวจว่าขนาดใหม่ยังคลิกตรง / Keep the gameplay root unchanged. Selection rings and click radii do not automatically scale with the visual.

### D. ยก/เลื่อน/หมุนเฉพาะภาพ / Correct visual offsets

ปรับ `Visual.localPosition` เพื่อให้ล้อ/เท้าอยู่พื้น และ `Visual.localRotation` เพื่อชดเชยแกน forward ของโมเดล อย่า hard-code ยก root ไป y>0 เพราะ simulation กำหนด destination.y=0 / Correct a child transform rather than lifting or rotating the gameplay coordinate system.

<a id="gameplay-boundary"></a>
## 11. ภาพ 3D ไม่เท่ากับกฎเกม / Visuals versus gameplay

| ปรับสิ่งนี้ / Changing this | ไม่ได้เปลี่ยนสิ่งนี้อัตโนมัติ / Does not automatically change |
| --- | --- |
| ขนาดรถ / Vehicle scale | HP, armor, damage, range, click radius |
| จำนวนคน / Visible soldier count | จำนวน formation, HP หรือ upkeep / Formation count, HP, upkeep |
| ปืนหัน / Barrel orientation | เป้าหมายยิง / Target selection |
| เพิ่ม collider | วิธีเลือกหน่วยหรือ pathfinding / Selection or pathfinding |
| โมเดล sandbag / Sandbag mesh | จุดกำบังใน list / Cover-point data |
| วง relay | ระบบยึดพื้นที่ / Objective capture |
| Animator | ความเร็วเดินหรืออัตรายิง / Gameplay speed or reload |

**TH:** การเลือกหน่วยปัจจุบันใช้ ray ชน `Plane(y=0)` แล้วหาระยะไป root ฝ่ายเรา `< 6` world units ไม่ได้ `Physics.Raycast` ใส่ collider หน่วย จึงไม่ต้องใส่ MeshCollider เพื่อให้คลิกได้ เส้น tracer เป็น cube จาก root ผู้ยิง y+1.5 ไป root เป้าหมาย y+1 อายุ .09 วินาที ไม่ใช่กระสุนฟิสิกส์และไม่ได้เริ่มที่ปลายปืน

**EN:** World selection intersects a y=0 plane and finds a friendly root within 6 units; it does not raycast against model colliders. A shot tracer is a short-lived cube between root-based offsets, not a ballistic projectile or a muzzle-based shot. Turrets do not independently track targets.

<a id="external-models"></a>
## 12. ถ้าหมายถึงโมเดลที่ gen จากภายนอก / Externally generated models

**TH:** ยังไม่ได้รับไฟล์โมเดลจากภายนอกในงานนี้ จึงไม่ยืนยัน rig, texture, license หรือความเข้ากันได้ของโมเดลใด ขั้นตอนนี้เป็นแนวทางใช้ไฟล์ที่ผู้ใช้มีสิทธิ์ใช้ ไม่ใช่การเพิ่ม AI service/SDK หรือการดาวน์โหลด asset ให้แล้ว

**EN:** No external generated model was supplied or integrated in this task. Its license, rig, textures and compatibility cannot be certified here. This workflow concerns assets you have rights to use; it does not install an AI generation service or importer.

### ขั้นตอนเตรียม asset / Preparation workflow

1. **License/provenance:** เก็บชื่อไฟล์ต้นทาง เครื่องมือที่สร้าง เงื่อนไขใช้งาน วันที่ และข้อจำกัด redistribution ห้ามถือว่า AI-generated = ใช้ได้ทุกกรณี / Record provenance and redistribution rights; generated does not automatically mean unrestricted.
2. **Format:** ใช้ไฟล์ FBX หรือ OBJ ที่ตรวจเปิดได้ในเครื่องมือ 3D; OBJ ไม่มี skeletal animation ส่วน GLB/glTF ต้องมี importer ที่ตรวจยืนยันก่อนเพราะโปรเจกต์นี้ยังไม่มี / Use a verified importable format. OBJ does not carry skeletal animation; this project has no established glTF/GLB importer.
3. **Geometry:** ตรวจ normals, UV, จำนวนวัสดุ, mesh ที่ซ้อนกัน, รูที่ไม่ตั้งใจ, ชิ้นส่วนลอย และ scale ในเครื่องมือสร้าง / Inspect normals, UVs, material slots, duplicates, holes, floating pieces and scale.
4. **Orientation:** ทำให้ด้านหน้าสุดท้ายใน Unity ตรง +Z, ขึ้น +Y, pivot กึ่งกลางฐาน และเท้า/ล้ออยู่ราว y=0 / Normalize final Unity orientation and ground pivot.
5. **Import:** สร้างโฟลเดอร์ art ของคุณใน Project window แล้วนำ model/textures เข้า รอ import ตรวจ Console และ Model Inspector ไม่เปลี่ยนทั้งโปรเจกต์เป็น URP เพียงเพราะไฟล์เดียวเป็นสีชมพู / Import model/textures and inspect the resulting asset before changing project rendering settings.
6. **Materials:** สร้าง `.mat` assets ใน Built-in pipeline แล้วต่อ base-color/normal maps ตาม shader ที่เลือก; FieldSurface ปัจจุบันไม่มี texture slots จึงต้องใช้ material/shader อื่นที่รองรับเมื่อมี textures / Use authored compatible materials; the existing FieldSurface cannot display supplied textures.
7. **Rig:** ถ้าเป็นรถไม่มี skeleton ใช้ model static ได้; ทหารที่ต้องการ animation ต้องมี rig/clip/Animator setup ที่เข้ากันจริง ไม่สร้างขึ้นจากการเปิด checkbox / Static vehicles do not need rigs; animated soldiers need a valid rig and animation setup.
8. **Prefab:** ประกอบ `ModelRoot → Visual` โดย ModelRoot scale `(1,1,1)` ใส่การชดเชย scale/rotation ใน Visual แล้วทดสอบขนาดเทียบรถต้นแบบที่ยาวราว 4 units / Wrap and size against existing vehicle geometry rather than guessing world dimensions.
9. **Colliders/physics:** ไม่เพิ่ม Rigidbody แบบ dynamic บน root ที่ simulation เคลื่อนด้วย Transform; ถ้าเพิ่ม collider เพื่อระบบใหม่ ต้องออกแบบ physics/selection/navigation ให้สอดคล้อง / Do not add dynamic Rigidbody motion on top of transform-driven movement without redesigning ownership.
10. **Integration:** การลาก prefab เข้า Main scene เพียงอย่างเดียวไม่แทนหน่วยที่ spawn; ต้องปรับ SpawnSide ตามหัวข้อถัดไป / A prefab placed in the entry scene does not replace runtime-spawned units.

<a id="prefab-integration"></a>
## 13. ตัวอย่างแทนที่ด้วย prefab / Optional prefab integration

> **TH:** เป็นตัวอย่างแก้ต่อในอนาคต **ยังไม่ได้ติดตั้งใน runtime** ไม่มี prefab หกไฟล์นี้ในโปรเจกต์ปัจจุบัน ไม่ต้องทำขั้นตอนนี้ถ้าจะใช้ procedural models เดิม
>
> **EN:** This is an optional code change, **not an installed feature**. The following prefab assets are proposed, not delivered.

สำหรับตัวอย่าง Resources แบบง่าย สร้าง prefab assets ของคุณภายใต้ `Assets/WW3War/Resources/WW3War/Models/` โดยชื่อ / For this simple example, author your own assets under that directory using these names:

```text
Infantry_Team1.prefab     Infantry_Team2.prefab
Armor_Team1.prefab        Armor_Team2.prefab
Artillery_Team1.prefab    Artillery_Team2.prefab
```

แต่ละ prefab ควรมี visual geometry/material references เท่านั้น ไม่ติด `WW3WarGame` หรือ runtime bootstrap ซ้ำ แยก prefab ของสองทีมช่วยกำหนดสีได้โดยไม่เปลี่ยน sharedMaterial ตอนรัน / Keep prefabs visual-only; do not attach duplicate game controllers. Separate team variants can retain authored team materials.

ใน `SpawnSide` ให้แทนที่บรรทัดเรียก UnitModel เดิมด้วย / Replace only the existing UnitModel call in SpawnSide:

```csharp
string resourceName = "WW3War/Models/" + roster[i].Kind + "_Team" + team;
var prefab = Resources.Load<GameObject>(resourceName);
if (prefab != null)
{
    var visual = Instantiate(prefab, root.transform, false);
    visual.name = "Visual";
    visual.transform.localPosition = Vector3.zero;
    visual.transform.localRotation = Quaternion.identity;
    visual.transform.localScale = Vector3.one;
}
else
{
    // Retain the original prototype when that prefab has not been authored.
    ProceduralFactory.UnitModel(root.transform, roster[i].Kind, team);
}
```

**TH:** วาง code ใน `WW3WarGame` ซึ่งเป็น MonoBehaviour จึงใช้ `Instantiate` ได้ ไม่ต้องใส่นามสกุล `.prefab` ใน Resources key; key เริ่มหลังโฟลเดอร์ Resources ตรวจตัวพิมพ์ให้ตรงตั้งแต่แรก ตัวอย่าง reset transform ของ root prefab จึงต้องใส่ scale/offset ที่ child Visual ภายใน prefab ไม่ใช่บน prefab root

**EN:** This snippet belongs inside the existing MonoBehaviour. Resources keys omit the extension and begin after the Resources directory. Match casing. Because the snippet resets the instantiated root transform, keep authored correction transforms on a child inside the prefab.

**TH:** ยังคงสร้าง/เก็บ gameplay root เดิมและ `Stats`, `Health`, `Destination`, `Source` ตามเดิม ไม่เปลี่ยน save schema ไม่ทำให้หน่วยถูกเพิ่มสองครั้ง และควร cache prefab lookup เมื่อขยายระบบ Resources เป็นแนวทางง่ายสำหรับต้นแบบ ไม่ใช่ข้อเสนอว่าได้ติดตั้ง Addressables แล้ว

**EN:** Preserve the existing gameplay root, statistics, source and registration. Replacing visuals need not change saves. Avoid spawning both prefab and procedural geometry simultaneously. For growth, cache asset lookups and choose an asset-loading approach deliberately; Addressables is not installed here.

<a id="persistence"></a>
## 14. เก็บเป็น asset และ export / Persistence and export

### สิ่งที่ทำได้ตอนนี้ / Current behavior

- ปรับ code แล้วเริ่ม operation ใหม่เพื่อสร้างซ้ำ / Edit source and regenerate on a new operation.
- save campaign เก็บข้อมูล formation ไม่เก็บ mesh/material/Transform hierarchy / Campaign saves store formation data, not geometry or tactical hierarchies.
- ไม่มีปุ่ม Bake Prefab, Export FBX หรือ Export GLB ในโปรเจกต์ / No prefab baker or model exporter is provided.

### ถ้าต้องการ prefab ถาวร / If you need persistent prefabs

**TH:** อย่าถือว่าลาก runtime root เข้า Project แล้ว material ที่สร้างด้วย `new Material` จะถูกบันทึกถูกต้องเอง Workflow ที่เชื่อถือได้ต้องมี Editor utility ที่สร้าง object ใน authoring context, บันทึก Material เป็น `.mat` asset ด้วย AssetDatabase, แทน references ของ Renderer ให้เป็น asset, แล้วใช้ PrefabUtility บันทึก prefab พร้อมจัดการชิ้นส่วนชั่วคราวและ Undo ให้ถูกต้อง

**EN:** Dragging a runtime root into the Project is not a complete persistence workflow for transient materials. A reliable baker needs to create authoring objects, persist material assets, remap renderer references, save the prefab through PrefabUtility, and clean up temporary objects with proper Editor/Undo semantics.

**TH:** built-in primitive meshes ต่างจาก mesh ที่สร้างเอง หากเพิ่ม custom Mesh ต้องบันทึก mesh assets ด้วย ไม่ควรใช้ factory เดิมใน Edit Mode โดยตรงเพราะการลบ collider ใช้ `Object.Destroy` ถ้าสร้าง utility นี้ต้องแยก Editor code ไว้ใน Editor folder และทดสอบหลังเปิดโปรเจกต์ใหม่จริง

**EN:** Persist custom meshes as well if introduced. The current factory's runtime collider cleanup is not an Edit Mode baker. Keep any new authoring utility Editor-only and verify persistence by restarting the Editor.

การส่งไป Blender/โปรแกรมอื่นต้องมี exporter ที่รองรับจริงและตรวจสิทธิ์ใช้ก่อน ไม่มีการติดตั้ง exporter หรือรับประกันว่า custom shader จะส่งออกไปเหมือนภาพใน Unity / Export needs a verified exporter and separate material handling; custom shader appearance is not guaranteed to survive export.

<a id="animation"></a>
## 15. Animation และจุดยิง / Animation and muzzle points

**Current / ปัจจุบัน:** ไม่มี rig, clips, Animator, turret tracking หรือ socket ปลายปืน การเคลื่อนใช้ Transform ของ root และ tracer ใช้ตำแหน่ง root ตามหัวข้อ 11 / No rigs, clips, animators, turret tracking or muzzle sockets exist; movement and tracers are root-based.

**Optional next design / แนวทางเพิ่มเอง:**

```text
GameplayRoot                    ← simulation owns movement
└── Visual                      ← visual scale/offset correction
    ├── Hull / Body
    ├── TurretPivot             ← unscaled articulation transform
    │   ├── TurretMesh
    │   └── GunPivot
    │       ├── GunMesh
    │       └── Muzzle          ← intended shot origin
    └── Animator (if needed)
```

- ปิด root motion หรือแยกการควบคุมให้ชัด เพราะ runtime เคลื่อน root เอง / Disable root motion or explicitly reconcile movement ownership.
- ถ้า Animator ใช้ `Time.deltaTime` ปกติ มันจะไม่หยุดตาม `battlePaused` และไม่เร่งตาม `battleSpeed` อัตโนมัติ เพราะเกมนี้ไม่ได้เปลี่ยน `Time.timeScale` ต้องเชื่อมค่ากับ animation เอง / Pause/speed are custom simulation controls; synchronize animation explicitly.
- Particle/audio ก็ต้องกำหนดพฤติกรรม pause/speed แยก ไม่ใช่แค่เพิ่ม component / Define pause/speed behavior for particles and audio as well.
- ถ้าเพิ่ม Muzzle ต้องแก้ DrawTracer ให้ใช้ socket และมี fallback ไม่ใช่ตั้งชื่อ object แล้ว tracer จะรู้เอง / Change tracer code to consume the socket, with a fallback.

<a id="performance"></a>
## 16. ประสิทธิภาพและต้นทุน / Performance and cost

ตัวเลขต่อไปนี้เป็น **จำนวน primitive renderers จากการนับ code ไม่ใช่ draw calls/FPS ที่วัดได้** / These are source-derived primitive renderer counts, not measured draw calls or FPS:

| Case | Count |
| --- | ---: |
| One infantry formation | 25 |
| One armor/artillery formation | 8 |
| Terrain including relay decorative ring | 165 |
| Reused unit selection ring | 32 |
| Example: 12 infantry + terrain + selection | `12 × 25 + 165 + 32 = 497` (before enemies/tracers) |

**TH:** ศัตรูและ tracer เพิ่มจำนวนอีก Collider ของ primitive ถูกสร้างก่อนแล้ว disable/destroy ไม่ได้แปลว่าไม่เคยสร้างเลย วัสดุแชร์ช่วยลดจำนวน material instances แต่ไม่รับประกันว่า renderer ทั้งหมด batch เป็น draw call เดียว

**EN:** Enemies and tracers add more. Primitive colliders are initially created, then disabled/destroyed. Shared materials reduce material-instance duplication but do not guarantee a single batched draw call.

แนวทางเมื่อมี Editor ให้ใช้ / When an Editor is available:

1. วัด Profiler/Rendering ใน battle ตัวแทนจริงทั้งก่อนและหลังแก้ / Profile representative battles before and after changes.
2. ดู GameObject/Renderer count, draw calls, CPU/GPU frame time, GC allocations และ memory / Track object count, render cost, timing, allocations and memory.
3. ลดชิ้นส่วนซ้ำ/ใช้ mesh รวมเฉพาะที่ไม่ต้องขยับแยก; พิจารณา LOD/instancing หลังมีข้อมูล / Consider merging static visual pieces, LOD or instancing based on measurements.
4. Tracer ปัจจุบัน Create/Destroy ต่อ shot; pooling เป็นงานปรับปรุงถัดไป ไม่ใช่ของที่ทำแล้ว / Shot pooling is future work, not an existing feature.
5. อย่าเพิ่ม Material/สีไม่จำกัดต่อ frame; แยก ownership ของ cache และ cleanup / Avoid unbounded per-frame materials and define cache lifetime.

ไม่มี benchmark หรือข้อกำหนด FPS ใหม่ในงานคู่มือนี้ / This documentation task introduces no FPS guarantee or measured optimization claim.

<a id="troubleshooting"></a>
## 17. แก้ปัญหา / Troubleshooting

| อาการ / Symptom | ตรวจอะไร / Check |
| --- | --- |
| Scene ว่างก่อน Play / Empty entry scene | เป็นปกติ; ต้องเริ่ม operation จึงสร้าง battlefield / Expected; start an operation to generate the battlefield |
| ลาก model asset ไม่ได้ / No model asset to drag | โมเดลเดิมสร้างตอนรัน ไม่มี FBX/prefab asset / Current models are runtime geometry |
| โมเดลสีชมพู / Pink geometry | ตรวจ shader resource, Console และ Built-in pipeline; imported materials อาจไม่ compatible / Check shader/resource/pipeline compatibility |
| Texture ไม่แสดง / Missing textures | FieldSurface ไม่มี texture slots; ใช้ material ที่รองรับและ assign maps / Choose a compatible textured material |
| หมุนแสงแล้วสีไม่เปลี่ยน / Light rotation has no effect | FieldSurface ใช้ทิศแสงคงที่ / Shader uses a hard-coded direction |
| ปืนใหญ่/รถขนาดผิด / Distorted gun size | Turret nonuniform scale ถูกสืบทอด / Inspect inherited turret scale |
| รถถอยหน้าหรือจมพื้น / Backward or sunken model | แก้ visual child orientation/pivot/scale ไม่ใช่ gameplay root / Correct the visual child |
| คลิกไม่ตรง / Misaligned selection | ระยะคลิกอิง root ไม่อิง collider; mesh offset/scale อาจมากเกิน / Selection is root-distance based |
| เดินทะลุโกดัง / Walks through warehouses | ไม่มี pathfinding/physical obstruction ในระบบปัจจุบัน / Current scenery is nonblocking |
| แก้สีชิ้นเดียวแต่เปลี่ยนหลายชิ้น / Recolor affects others | ใช้ shared material เดียวกัน / Shared material mutation |
| เปลี่ยนขณะ Play แล้วหาย / Edits disappear after Play | runtime object ไม่ใช่ authored asset / Persist changes in source or authored assets |
| ย้าย sandbag แต่กำบังอยู่ที่เก่า / Cover does not move with mesh | เปลี่ยน cover data พร้อม visuals / Update the cover-point list too |
| โมเดลซ้อนสองชุด / Duplicate geometry | เรียก UnitModel ซ้ำหรือ instantiate prefab แล้วยังเรียก factory / Spawn one visual path only |
| สร้างภาพแล้วหน่วยไม่รบ / Visual does not participate | ยังไม่ได้ลงทะเบียน `BattleUnit`; helper แค่สร้างภาพ / Register through the battle spawn flow |
| Animation ยังเล่นตอน Pause / Animations ignore pause | custom battle pause ไม่ได้หยุด Animator อัตโนมัติ / Synchronize animation with simulation controls |
| Prefab รีเปิดแล้ววัสดุหาย / Materials lost after restart | transient materials ไม่ได้บันทึกเป็น `.mat` assets / Persist and remap material assets |

<a id="acceptance"></a>
## 18. Checklist ก่อนส่งโมเดล / Acceptance checklist

ใช้กับการเปลี่ยนภาพทุกครั้ง ไม่ทำเครื่องหมายว่าผ่านก่อนทดสอบจริง / Complete this in the actual Editor/player before declaring model integration successful:

- [ ] Unity imports without compile/shader errors; models are visible in an actual battle.
- [ ] มีหน่วยทั้งสามชนิดสองฝ่าย สีระบุฝ่ายตรง / All three kinds on both teams have correct identification colors.
- [ ] Forward +Z, ground alignment, scale and pivot match the gameplay root.
- [ ] เลือกจาก world/card/1–9 ได้ วงเลือกตรงภาพ / Selection and its marker align with the visual.
- [ ] Move, hold, camera, automatic attacks and death work after changing visuals.
- [ ] Visual size changes do not unintentionally change HP/range or save data.
- [ ] ที่กำบังตรงกับข้อมูลระยะจริง / Cover visuals agree with cover-point data.
- [ ] ถ้ามี Animator/VFX ต้องทดสอบ pause/resume และ 1×/2× / Animation/VFX follow simulation controls.
- [ ] End battle and start another without duplicate objects or growing unintended material instances.
- [ ] ถ้าใช้ prefab ให้ปิดเปิด Editor ใหม่และตรวจ material/mesh references / Persisted assets survive an Editor restart.
- [ ] Build Windows and test the actual player; do not infer shader/prefab inclusion from Editor appearance alone.
- [ ] บันทึก provenance/license ของ assets ภายนอกและผล profiling ถ้ามีการอ้างประสิทธิภาพ / Record external-asset rights and measure any performance claims.

**ขอบเขตการส่งมอบคู่มือนี้ / Documentation delivery:** เพิ่มเอกสารและลิงก์อ้างอิงเท่านั้น ไม่ได้สร้าง FBX, ติดตั้ง importer/exporter, bake prefab หรือเปลี่ยน runtime ให้โหลด asset ภายนอก / This delivery adds documentation and navigation links only; it does not generate FBX files, install importers/exporters, bake prefabs or modify runtime asset loading.
