# WW3 WAR — Unity Development Guide / คู่มือพัฒนาเกม Unity

**ภาษา / Languages:** ไทย และ English<br>
**Unity:** `6000.0.84f1` (ตาม `ProjectSettings/ProjectVersion.txt`)<br>
**สถานะ / Status:** playable prototype; original, procedural presentation; not a commercial-ready game.

---

## 1. ภาพรวม / Overview

**TH:** WW3 WAR เป็นเกมวางแผนสงครามสมัยใหม่ต้นแบบที่เขียนด้วย Unity/C# มีสองส่วน: แคมเปญแบบผลัดเทิร์นเพื่อยึดพื้นที่ และการรบ 3D แบบเรียลไทม์ที่ผู้เล่นสั่งกองกำลังเอง ฉาก แผนที่ และโมเดลต้นแบบสร้างจากโค้ด จึงไม่ต้องนำ asset จาก Total War หรือเกมอื่นมาใช้

**EN:** WW3 WAR is an original modern-warfare strategy prototype built with Unity/C#. It combines a turn-based theater campaign with player-directed real-time 3D battles. The prototype scene, map, and unit models are generated in code; it does not include Total War or other third-party game assets.

### ขอบเขตต้นแบบ / Prototype scope

| มีแล้ว / Included | ยังไม่รวม / Not included yet |
|---|---|
| แผนที่ 10 จังหวัด, การเชื่อมต่อ, ฝ่าย, รายได้ / 10-province connected map, ownership, income | Multiplayer, online services, controller support |
| เทิร์น, action, supply, income/upkeep, recruitment / Turns, actions, supply, income/upkeep, recruitment | Terrain pathfinding, animations, sound |
| หน่วยทหารราบยานยนต์, ยานเกราะ, ปืนใหญ่อัตตาจร / Infantry, armor, mobile artillery | Diplomacy, technology tree, naval/air units |
| สั่งเคลื่อนที่และยิงอัตโนมัติเมื่ออยู่ในระยะ / Move orders and automatic fire in range | Production art, campaign map texture, localization UI |
| Save/load แบบ local และตรวจข้อมูลเซฟ / Local save/load with save validation | Cloud saves, multiple save slots, accessibility options |

> ค่าสถานะหน่วยเป็น **ค่าบาลานซ์เกมแบบอาร์เคด** ไม่ใช่ข้อมูลหรือคำแนะนำทางทหารในโลกจริง<br>
> Unit attributes are **arcade game-balance values**, not real-world military data or advice.

---

## 2. เปิดและเล่น / Open and play

### ไทย

1. ติดตั้ง Unity Editor เวอร์ชัน `6000.0.84f1` ผ่าน Unity Hub หากยังไม่มี (โปรเจกต์นี้ไม่ติดตั้ง Editor ให้เอง)
2. เปิดโฟลเดอร์โปรเจกต์นี้ใน Unity Hub แล้วเปิดฉาก `Assets/WW3War/Scenes/Main.unity`
3. กด **Play** — `WW3WarGame` เริ่มทำงานอัตโนมัติ แม้ฉากเริ่มต้นจะว่าง
4. สร้าง Windows build ได้ด้วย `powershell -ExecutionPolicy Bypass -File .\tools\Build-Windows.ps1` เมื่อ Editor ติดตั้งแล้ว หรือเลือกเมนู `WW3 War > Build Windows`

### English

1. Install Unity Editor `6000.0.84f1` using Unity Hub if needed. This project does not install Unity for you.
2. Add this project folder in Unity Hub and open `Assets/WW3War/Scenes/Main.unity`.
3. Press **Play**. `WW3WarGame` bootstraps itself at runtime, even though the starting scene is intentionally empty.
4. Build Windows with `powershell -ExecutionPolicy Bypass -File .\tools\Build-Windows.ps1` after the Editor is installed, or use `WW3 War > Build Windows`.

**ข้อจำกัดการตรวจสอบ / Verification note:** The standalone .NET Core suite passed **20 checks**. These do not test Unity rendering, input, or Unity `JsonUtility`. Unity Editor `6000.0.84f1` is not present in the standard Hub locations checked here, so Unity import/Play Mode and the player build remain unverified. / ชุดทดสอบ Core บน .NET ผ่าน **20 checks** แต่ไม่ได้ทดสอบภาพ อินพุต หรือ `JsonUtility` ของ Unity และไม่พบ Unity Editor ที่ต้องใช้ จึงยังไม่ยืนยันการ import, Play Mode หรือ build จริง

---

## 3. วิธีเล่น / How to play

### แคมเปญ / Campaign

| การกระทำ / Action | วิธี / How |
|---|---|
| เลือกจังหวัด / Select province | คลิกชื่อจังหวัดบนแผนที่ / Click its map label |
| เคลื่อนทัพ / Move army | เลือกจังหวัดฝ่ายเรา adjacent แล้วกด Move; ใช้ 3 supply และ 1 action / Select an adjacent friendly province and click Move; costs 3 supply and 1 action |
| เปิดปฏิบัติการ / Launch operation | เลือกจังหวัดเป็นกลางหรือศัตรูที่เชื่อมติดกันแล้วกด Launch; ใช้ 8 supply และ 1 action / Attack an adjacent neutral or hostile province; costs 8 supply and 1 action |
| เกณฑ์หน่วย / Recruit | เลือก Infantry, Armor หรือ Artillery ที่แผง Command / Use the reinforcement buttons in Command |
| จบเทิร์น / End turn | รับรายได้ จ่าย upkeep ฟื้นฟู/เสียกำลังจากเสบียง และศัตรูอาจขยายพื้นที่ / Collect income, pay upkeep, recover or suffer attrition, and allow enemy expansion |
| บันทึก / Save | Save Campaign เขียนไฟล์ใน `Application.persistentDataPath` / Save Campaign writes under `Application.persistentDataPath` |

### การรบ / Battle

- คลิกซ้ายที่หน่วยสีน้ำเงินหรือเลือกจากแถบล่าง; กด `1`–`9` เลือกตามลำดับ / Left-click a blue formation or select it from the roster; press `1`–`9` for numbered formations.
- คลิกขวาบนพื้นเพื่อสั่งเคลื่อนที่; `F` ให้หน่วยหยุดเคลื่อน / Right-click the field to move; press `F` to hold position.
- หน่วยยิงเป้าหมายศัตรูที่ใกล้ที่สุดโดยอัตโนมัติเมื่ออยู่ในระยะ เกราะลดความเสียหาย และที่กำบังระยะ 5 เมตรลด damage เพิ่ม / Units auto-fire at the nearest enemy in range. Armor reduces damage; cover within 5 m reduces it further.
- `Space` หรือปุ่ม Pause หยุด/เล่นต่อ; ปุ่ม speed สลับ `1×/2×` / `Space` or Pause toggles pause; the speed button switches `1×/2×`.
- กล้อง: `W A S D` เคลื่อนที่, `Q/E` หมุน, ล้อเมาส์ซูม / Camera: `W A S D` pan, `Q/E` rotate, mouse wheel zoom.
- กด Withdraw เพื่อถอนกำลังและบันทึกผลการรบเป็นการแพ้ / Withdraw to retreat and record a loss.

---

## 4. โครงสร้างโค้ด / Code structure

| Path | Responsibility / หน้าที่ |
|---|---|
| `Assets/WW3War/Scripts/Core/CampaignModels.cs` | Serialized campaign, province, formation, battle, and result data / โมเดลข้อมูลเกม |
| `Assets/WW3War/Scripts/Core/CampaignRules.cs` | Deterministic campaign rules, legality checks, save validation / กติกาแคมเปญและตรวจเซฟ |
| `Assets/WW3War/Scripts/Core/UnitCatalog.cs` | Original unit names, costs, upkeep, and arcade combat stats / ข้อมูลและค่าบาลานซ์หน่วย |
| `Assets/WW3War/Scripts/Core/CombatRules.cs` | Armor and cover damage calculation / คำนวณ damage จากเกราะและที่กำบัง |
| `Assets/WW3War/Scripts/Runtime/WW3WarGame.cs` | Runtime bootstrap, IMGUI, campaign flow, battle simulation, persistence / เริ่มเกม, UI, การรบ, บันทึกเกม |
| `Assets/WW3War/Scripts/Runtime/ProceduralFactory.cs` | Procedural materials, terrain, and placeholder unit geometry / สร้างฉากและโมเดลชั่วคราว |
| `Assets/WW3War/Scripts/Runtime/RTSCamera.cs` | RTS camera input / ควบคุมกล้อง |
| `Assets/WW3War/Scenes/Main.unity` | Minimal boot scene / ฉากเริ่มต้นแบบ minimal |
| `Assets/WW3War/Editor/BuildCommands.cs` | Unity menu and Windows build entry point / คำสั่ง build ใน Editor |

### Runtime flow / ลำดับการทำงาน

1. `[RuntimeInitializeOnLoadMethod]` creates the game controller after the scene loads. / สร้าง game controller หลังฉากโหลด
2. `CampaignRules.NewCampaign()` supplies the initial state; UI actions call the same rules API used by tests/tools. / เริ่มสถานะแคมเปญผ่าน Core rules
3. An attack creates a `BattleSpec`; the runtime builds a new procedural field and combat actors. / คำสั่งโจมตีสร้างสนามและหน่วยจาก `BattleSpec`
4. Battle survivors become a `BattleResolution`. `CampaignRules.ResolveBattle` validates the committed roster and applies the result. / แปลงผู้รอดเป็นผลรบ แล้วให้ Core ตรวจและบันทึกผล
5. Save files use Unity `JsonUtility`; input is size-limited and validated before replacing campaign state. Writes use a temporary file and preserve a backup. / เซฟด้วย `JsonUtility` จำกัดขนาด ตรวจข้อมูล และเขียนผ่าน temporary พร้อมสำรองไฟล์เดิม

หลักการพัฒนา / Design rule: **keep campaign rules independent of Unity objects**. Put new economy, province, or battle-commit rules in `Core`; keep rendering, input, and scene objects in `Runtime`. / แยกกติกาเกมออกจาก Unity scene objects เสมอ

---

## 5. เพิ่มเนื้อหา / Extending the prototype

### เพิ่มชนิดหน่วย / Add a unit type

1. Add the enum value to `UnitKind` in `CampaignModels.cs`.
2. Add balanced values in `UnitCatalog.Get` and include it in `IsValid`.
3. Add its procedural appearance in `ProceduralFactory.UnitModel`.
4. Add a recruitment option and check cost, upkeep, battle behavior, and save validation.

### เพิ่มจังหวัด / Add a province

1. Add it to `CampaignRules.CreateMap` with a unique ID, normalized map coordinates (`0..1`), income, owner, and neighbor IDs.
2. Update the expected province count in `CampaignRules.Validate` (currently exactly 10).
3. Keep neighbor links reciprocal; add campaign tests for movement, attacks, expansion, victory/defeat, and invalid saves.

### งานแนะนำก่อน production / Recommended before production

- Replace IMGUI with authored UI (Canvas/UI Toolkit), add keyboard/controller remapping and Thai/English localization resources.
- The standalone rule-check project is `Tests/WW3War.CoreChecks`; it covers campaign, battle-result validation, and combat rules but does not test Unity rendering/input or Unity `JsonUtility` behavior. / ชุดตรวจ Core แบบ standalone อยู่ที่ `Tests/WW3War.CoreChecks` แต่ไม่ทดสอบ rendering/input ของ Unity หรือ `JsonUtility` จริง
  Build its checks with `dotnet build .\Tests\WW3War.CoreChecks\WW3War.CoreChecks.csproj`. / ใช้คำสั่งนี้ compile ชุดตรวจ Core
- Add pathfinding and obstruction-aware movement; battle currently moves directly toward destinations.
- Add combat feedback (projectiles, impact VFX, sound, suppression, morale, formations) and performance profiling for larger armies.
- Add options, pause, confirmation dialogs, multiple save slots, and crash-safe atomic writes.
- Use original or properly licensed art/audio/fonts. Keep third-party notices and licenses in the distribution.

---

## 6. ตรวจสอบก่อนส่งงาน / Release checklist

- [ ] Unity opens the project at the pinned editor version without compile errors. / เปิดโปรเจกต์ได้และไม่มี compile error
- [ ] Play mode: move, recruit, end turn, attack, win/withdraw, return to campaign. / ทดสอบวงจรแคมเปญและการรบครบ
- [ ] Save/load works; malformed or altered campaign saves are rejected. / เซฟโหลดได้และปฏิเสธข้อมูลผิดรูป
- [ ] Victory and defeat states prevent invalid actions. / สถานะชนะ/แพ้ปิดการกระทำที่ไม่ถูกต้อง
- [ ] Test 16:9, 16:10, and 4:3 resolutions plus minimum supported resolution. / ทดสอบหลายอัตราส่วนและความละเอียดขั้นต่ำ
- [ ] Build the full Windows player folder and test on a clean machine. / build ทั้งโฟลเดอร์และทดสอบในเครื่องสะอาด
- [ ] Review licenses, attribution, save location, and accessibility before distribution. / ตรวจลิขสิทธิ์ เครดิต ตำแหน่งเซฟ และ accessibility

---

## 7. ที่มาและข้อจำกัด REA / REA research provenance and limits

The companion report is [`reverse-engineering/Findings-TH-EN.md`](reverse-engineering/Findings-TH-EN.md). It separates stored REA observations from design choices. This prototype borrows only broad strategy-game ideas (territory, turns, recruitment, tactical battle); it does not reproduce Rome II code, text, art, maps, or proprietary data. / รายงานแนบแยกหลักฐาน REA ที่มีอยู่กับการออกแบบต้นแบบ โดยนำมาใช้เพียงแนวคิดเกมวางแผนในระดับกว้าง ไม่คัดลอกโค้ด ข้อความ ภาพ แผนที่ หรือข้อมูลเฉพาะของ Rome II
