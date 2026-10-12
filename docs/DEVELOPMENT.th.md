# คู่มือพัฒนา ww3 war ใน Unity

[English](DEVELOPMENT.en.md) · [หน้าแรก](../README.md) · [คู่มือโมเดล 3D แบบละเอียด](3D-MODELS-TH-EN.md) · [รายงาน REA](reverse-engineering/REPORT.md)

## 1. เป้าหมายและสถานะ

**ww3 war** เป็นต้นแบบเกมสงครามสมัยใหม่แบบแคมเปญผลัดตาและการรบเรียลไทม์ ใช้จังหวัดและกองกำลังสมมติ เขียนระบบและ geometry ใหม่ ไม่ต้องติดตั้ง ROME II เพื่อเล่นต้นแบบ และไม่มีไฟล์เกมต้นฉบับในส่วนเกม Unity

มีระบบแคมเปญ เศรษฐกิจ เสบียง รับกำลังเสริม การรบ บันทึกผลกลับแคมเปญ และ save/load ไม่ใช่เกมระดับ AAA หรือการถอด source ของ ROME II รายงาน REA ระบุหลักฐานและข้อจำกัดแยกต่างหาก

**ผลตรวจปัจจุบัน:** ชุดทดสอบ core C# ผ่าน 20 รายการ แต่ยังไม่พบ Unity Editor ในตำแหน่งติดตั้งที่ตรวจ จึงยังไม่ได้ยืนยันการ import, Play Mode, shader หรือ build Windows อ่าน [VERIFICATION](VERIFICATION.md) ก่อนส่งต่อ

## 2. เตรียมเครื่องและเปิดโปรเจกต์

1. ใช้ Unity Hub ติดตั้ง **Unity 6000.0.84f1** พร้อม Windows Build Support (Mono) หากต้องการ build Windows
2. Add project from disk โดยเลือกโฟลเดอร์ที่มี `Assets`, `Packages`, `ProjectSettings` ไม่ใช่โฟลเดอร์ `Assets` เพียงอย่างเดียว
3. รอ Unity import ให้เสร็จ ตรวจ Console แล้วเปิด **WW3 War → Open Main Scene**
4. กด Play จะมีหน้าจอแคมเปญ ระบบสร้าง GameObject ขณะรันโดยอัตโนมัติ ไม่ต้องลาก component ไปใส่ฉาก
5. เปิดคู่มือนี้จาก **WW3 War → Documentation → Development Guide (Thai)** จะใช้โปรแกรมเปิด Markdown ของระบบ

ไฟล์กำหนดเวอร์ชัน: [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt) และ package ที่ใช้อยู่: [manifest.json](../Packages/manifest.json) ใช้ Built-in rendering, IMGUI, legacy Input และ Unity modules เดิม ไม่ต้องลง Asset Store package

เปิดด้วย PowerShell จาก root:

```powershell
./tools/Open-Unity.ps1 -UnityPath 'D:\YourUnityFolder\Editor\Unity.exe'
```

[Open-Unity.ps1](../tools/Open-Unity.ps1) ไม่ติดตั้งหรืออัปเดต Unity ให้เอง อย่าใช้ Unity Hub หรือ Unity CLI แทน Editor แม้ไฟล์ CLI จะชื่อ `Unity.exe` เหมือนกัน

## 3. โครงสร้างที่ต้องรู้

| ไฟล์ | หน้าที่ |
| --- | --- |
| [Main.unity](../Assets/WW3War/Scenes/Main.unity) | ฉากเริ่มต้นแบบเบา Runtime สร้างกล้อง แสง UI และสนาม |
| [CampaignModels.cs](../Assets/WW3War/Scripts/Core/CampaignModels.cs) | DTO แบบ `[Serializable]` และ public fields สำหรับ Unity JSON |
| [CampaignRules.cs](../Assets/WW3War/Scripts/Core/CampaignRules.cs) | กฎแคมเปญ, RNG, การตรวจ save, commit ผลรบ ไม่มี dependency กับ Unity |
| [UnitCatalog.cs](../Assets/WW3War/Scripts/Core/UnitCatalog.cs) | ราคาหน่วย ค่า upkeep และสถิติสมมติ |
| [CombatRules.cs](../Assets/WW3War/Scripts/Core/CombatRules.cs) | สูตรความเสียหาย เกราะ และที่กำบัง ไม่มี dependency กับ Unity |
| [WW3WarGame.cs](../Assets/WW3War/Scripts/Runtime/WW3WarGame.cs) | วงจรหน้าจอ input simulation UI save/load |
| [ProceduralFactory.cs](../Assets/WW3War/Scripts/Runtime/ProceduralFactory.cs) | โมเดลยานเกราะ ทหาร ถนน sandbag และฉากจาก primitives |
| [RTSCamera.cs](../Assets/WW3War/Scripts/Runtime/RTSCamera.cs) | กล้องมุมสูง pan/rotate/zoom |
| [FieldSurface.shader](../Assets/WW3War/Resources/FieldSurface.shader) | shader สี/แสงแบบเรียบง่ายสำหรับ Built-in pipeline |
| [BuildCommands.cs](../Assets/WW3War/Editor/BuildCommands.cs) | เมนูฉาก คู่มือ และ build Windows |
| [SmokeChecks.cs](../Assets/WW3War/Editor/SmokeChecks.cs) | ตรวจ asset, Unity JSON และการ commit ผลรบใน Editor |
| [Program.cs](../Tests/WW3War.CoreChecks/Program.cs) | ชุดตรวจ core ที่รันด้วย .NET โดยไม่จำลอง Unity API |

`Core` ต้องคงเป็น pure C# เพื่อทดสอบได้โดยไม่ใช้ Editor ส่วน `Runtime` รับผิดชอบภาพ เวลา input และ filesystem อย่าให้กฎเศรษฐกิจไปอยู่ในปุ่ม GUI หากต้องใช้จาก AI หรือระบบอื่นในอนาคต

## 4. วงจรแคมเปญและกฎปัจจุบัน

เริ่มที่ Aster Harbor: turn 1, credits 420, supply 85, actions 3 มีทหารราบ ยานเกราะ และปืนใหญ่ชนิดละหนึ่ง formation จังหวัดมี 10 แห่ง เจ้าของใช้ `0 = neutral`, `1 = player`, `2 = enemy` กองทัพผู้เล่นมีเพียงกองเดียว สูงสุด 12 formations

- เดินได้เฉพาะจังหวัดเพื่อนบ้านที่เป็นของฝ่ายเรา: ใช้ 1 action และ 3 supply
- โจมตีเพื่อนบ้านที่ยังไม่ใช่ของเรา: ใช้ 1 action และ 8 supply ตอนยืนยันเริ่มรบ
- รับหน่วยใหม่ใช้ credits และ 1 action ต้องอยู่ในจังหวัดเรา
- จบตา: รวมรายได้จังหวัดเรา ลบ upkeep จากทุกหน่วย, supply เพิ่ม 18 ลบ `จำนวนหน่วย × 2` แล้วจำกัด 0–100
- เมื่อเงินหรือเสบียงก่อนหักไม่พอ หน่วยเสีย health 18 จุดเปอร์เซ็นต์ มิฉะนั้นฟื้น 8 จุดเปอร์เซ็นต์ จำกัดไม่เกิน 100% หน่วย health ≤ 0 ถูกนำออก
- ทุกตาเลขคู่ ศัตรูยึดจังหวัดติดอาณาเขตตนหนึ่งแห่งได้ ยกเว้นจังหวัดที่กองทัพเรายืนอยู่ เป็นกฎ AI แบบย่อ ไม่ใช่กองทัพศัตรูที่เดินจริง
- ชนะเมื่อไม่มีจังหวัดฝ่ายศัตรูเหลือ และยังมีกองทัพกับดินแดนเรา จังหวัด neutral ไม่จำเป็นต้องยึดทั้งหมด แพ้เมื่อไม่เหลือหน่วยหรือดินแดน

### อินเทอร์เฟซหลัก

```csharp
CampaignState state = CampaignRules.NewCampaign(2026);
CampaignRules.MoveArmy(state, "vale", out string moveMessage);
CampaignRules.Recruit(state, UnitKind.Infantry, out string recruitMessage);
string turnMessage = CampaignRules.EndTurn(state);
bool ok = CampaignRules.Validate(state, out string validationMessage);
```

ตรวจค่า `bool` และข้อความทุกครั้งที่เรียกคำสั่ง DTO ที่รับมาจากไฟล์ต้องผ่าน `Validate` ก่อนนำไปใช้ การ validate เป็นการตรวจโครงสร้างและความสอดคล้อง ไม่ใช่ลายเซ็นป้องกันผู้เล่นแก้ save

## 5. ธุรกรรมการรบ

`TryBeginAttack` สร้าง `PendingBattle` ใน campaign และส่ง `BattleSpec.Copy()` ให้ runtime โดยมี battle ID, seed, จังหวัดต้นทาง/เป้าหมาย และ roster ก่อนรบ ระหว่าง pending จะห้ามเดิน รับหน่วย และจบตา

```csharp
if (CampaignRules.TryBeginAttack(state, "ridge", out BattleSpec battle, out string message))
{
    // ส่ง battle ให้ runtime; เก็บ Id/Kind ของหน่วยไว้
    var result = new BattleResolution {
        BattleId = battle.BattleId,
        ProvinceId = battle.ProvinceId,
        PlayerWon = true,
        DurationSeconds = 60f
    };
    foreach (var unit in battle.PlayerUnits)
        result.Survivors.Add(new FormationState(unit.Id, unit.Kind, unit.Health * 0.8f));
    CampaignRules.ResolveBattle(state, result, out string resultMessage);
}
```

ตัวอย่างเป็นการเรียก API ไม่ใช่ auto-resolve ที่อยู่ในเกมจริง ผลต้องมีเฉพาะหน่วยเดิมที่รอด ห้ามเพิ่ม health, เปลี่ยนชนิด, ส่ง ID ซ้ำ หรือส่งผลคนละ battle เมื่อ commit แล้ว `PendingBattle` ถูกล้างเพื่อกัน reward ซ้ำ ชนะได้ +45 credits และย้ายไปจังหวัดที่ยึด ถอนกำลังใช้ผลแพ้แต่เก็บหน่วยที่ยังมีชีวิต

## 6. การเล่นและการรบสมัยใหม่

เลือกจังหวัด → Launch Operation → เลือกหน่วยด้วยคลิกซ้ายบนหน่วยสีน้ำเงิน/การ์ด หรือปุ่ม 1–9 → คลิกขวาบนพื้นที่สนามเพื่อเดิน การ์ดเลือกหน่วยได้ครบทั้ง 12 ปุ่ม F หยุดเดิน หน่วยยิงศัตรูใกล้สุดอัตโนมัติในระยะ

WASD เลื่อนกล้อง, Q/E หมุน, wheel ซูม; Space หรือปุ่ม Pause หยุด simulation โดยกล้องยังขยับได้ Speed สลับ 1×/2× เวลารายงานหลังรบเป็นเวลาจำลอง Withdraw ถอนกำลังทันที

| หน่วย | ราคา | Upkeep | HP | Speed | Range | Damage | Reload | Armor |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Mechanized Infantry | 90 | 6 | 110 | 4.8 | 24 | 13 | 1.2 | 0.05 |
| Main Battle Armor | 150 | 12 | 230 | 6.2 | 32 | 35 | 2.5 | 0.30 |
| Mobile Artillery | 170 | 10 | 90 | 3.7 | 54 | 51 | 4.5 | 0 |

หน่วยระยะและความเร็วเป็น world units ของเกม ไม่ใช่ค่าจริงของอาวุธ สูตรยิง: `Damage × (1 - Armor ของเป้าหมาย) × CoverMultiplier` โดย multiplier = 0.65 เมื่อเป้าหมายอยู่ห่างจุด sandbag น้อยกว่า 5 units ทั้งสองฝ่ายใช้กฎเดียวกัน ปืนใหญ่ยังเป็น direct damage ไม่มีวิถีกระสุนหรือ splash

**ข้อจำกัดสำคัญ:** เคลื่อนที่เส้นตรง ไม่มี navmesh, collision, line of sight หรือ unit avoidance โกดังเป็นฉากประกอบ เสากลางไม่ใช่จุดยึดครอง ยังไม่มี morale/routing, deployment phase, เสียง, fog of war หรือ multiplayer อย่าสื่อว่าเป็นฟีเจอร์ที่ทำแล้ว

## 7. บันทึกเกม

ปุ่ม Save Campaign เขียน `ww3-war-campaign.json` ใน `Application.persistentDataPath` สำหรับ Windows โดยปกติอยู่ใต้ `%USERPROFILE%\AppData\LocalLow\WW3 War Studio\ww3 war` ตรวจค่าจริงจาก Unity หากเครื่องตั้งต่างออกไป

- JSON เป็น `CampaignState` โดยตรง ยังไม่มี version envelope
- fields สำคัญ: `Turn`, `Credits`, `Supply`, `Actions`, `ArmyProvinceId`, `Provinces`, `Army`, `Seed`, `Victory`, `Defeat`, `BattleSequence`, `RecruitmentSequence`, `PendingBattle`
- `FormationState.Health` เป็นสัดส่วน `(0,1]` ไม่ใช่ HP; `Kind` เป็น enum เลข 0/1/2
- เขียน `.tmp` ก่อน จากนั้นแทนไฟล์เดิมพร้อมเก็บ `.bak` ถ้ายังไม่มีไฟล์ให้ย้าย `.tmp` ไปเป็น save หลัก ความผิดพลาดจะปรากฏเป็น notice
- Load จำกัดขนาด 1 MiB และตรวจ `CampaignRules.Validate` ก่อนเปลี่ยน state ปัจจุบัน ไม่โหลด `.bak` อัตโนมัติ
- UI ห้าม save ระหว่างรบ หากโหลด JSON ที่มี valid pending battle จะเริ่มการรบนั้นจาก roster ตั้งต้นใหม่ ไม่ได้ resume ตำแหน่งกลางการรบ

ก่อนเปลี่ยน schema ให้เพิ่มเวอร์ชันและ migration ชัดเจน ทดสอบไฟล์เก่า/ขาด/ผิดชนิด/ซ้ำ ห้ามโยนข้อมูลผิดเข้าระบบรบโดยข้าม validation

## 8. ขยายเกมอย่างปลอดภัย

### เพิ่มหน่วย

เพิ่ม enum และ validation ใน core, เติม UnitCatalog, ออกแบบโมเดลใน factory, ปรับ RNG ที่ตอนนี้สุ่มสามชนิด, ปุ่ม recruit และข้อจำกัด save จากนั้นเพิ่ม regression checks อย่าเปลี่ยนลำดับเลข enum เดิม เพราะ save เก็บเป็นเลข

### เพิ่มจังหวัด

แก้ `CreateMap` ให้ ID ไม่ซ้ำ พิกัด 0–1 และเชื่อม neighbors ทั้งสองทาง ปรับ validator ที่คาด 10 จังหวัดและการทดสอบพร้อมกัน ข้อมูลแผนที่เป็นของเรา ไม่ใช้แผนที่ ROME II

### ปรับภาพและประสิทธิภาพ

เริ่มจาก replace primitive model ด้วย prefab ที่สร้างเองหรือมีสิทธิ์ใช้ แยก selection/UI จาก simulation ก่อนขยายจำนวนหน่วย factory ปิด collider ของ primitives ไว้ การเพิ่ม pathfinding ต้องมีข้อมูล navigation แยกต่างหาก ตรวจ Profiler บน workload เป้าหมายก่อนใช้ pooling/instancing หรือเปลี่ยนเป็น ECS ไม่มีการวัด FPS หรืออ้าง performance improvement ในการส่งมอบครั้งนี้

### เป้าหมายขั้นถัดไป

1. ทดสอบ Play Mode และ build จริง แล้วปรับ UI ที่ความละเอียดเป้าหมาย
2. Selection หลายหน่วยและ formation orders; health bars, minimap และ tutorial
3. Objective capture, morale และ suppression; save simulation ระหว่างรบ
4. เทคโนโลยี การทูต สิ่งปลูกสร้าง และกองทัพหลายกอง โดยแยก state ให้ทดสอบได้

## 9. การทดสอบ

### .NET checks (ไม่ต้องใช้ Unity)

ใช้ .NET SDK 10 จาก root:

```powershell
dotnet run --project Tests/WW3War.CoreChecks/WW3War.CoreChecks.csproj --configuration Release
```

[โปรเจกต์ทดสอบ](../Tests/WW3War.CoreChecks/WW3War.CoreChecks.csproj) link source core จริง ไม่ใช้ stub Unity และไม่ต้องใช้แพ็กเกจทดสอบภายนอก มี 20 checks รวมหลาย seed, movement, recruitment, economy, attrition, battle locking, duplicate results, survivors, corrupt state และ damage การ round-trip ใน .NET ใช้ `System.Text.Json` จึงไม่แทนการทดสอบ `JsonUtility`

### Editor smoke checks

เมนู **WW3 War → Run Smoke Checks** ตรวจ scene/shader asset, Unity JsonUtility, pending battle และผลรบ หรือใช้ Editor batch:

```powershell
& 'D:\YourUnityFolder\Editor\Unity.exe' -batchmode -nographics -quit -projectPath (Get-Location).Path -executeMethod WW3War.Editor.SmokeChecks.Run -logFile Logs/editor-smoke.log
```

ต้องตรวจ exit code กับ log ไม่ใช่ดูแค่ว่าสั่งเปิดได้ Smoke checks ไม่พิสูจน์ shader rendering หรือ input

### Checklist ที่ต้องทำใน Play Mode / player จริง

- Console ไม่มี compile error; เริ่มแคมเปญใหม่ เห็นจังหวัดครบและ sidebar เลื่อนได้ที่ 1280×720 และ 1600×900
- Recruit, move, end turn แล้วเครดิต เสบียง action และหน่วยเปลี่ยนถูกต้อง
- Launch Operation เห็นโมเดล/ถนนจริง ไม่ถูกพื้นหลัง UI บัง; คลิกและ 1–9 เลือกถูกหน่วย
- เดิน/hold, pan/rotate/zoom, pause/resume และ 2× ทำงาน ทั้งสองฝ่ายทำความเสียหายและหน่วยตายได้
- ทดสอบยิงทั้งในและนอกที่กำบัง ถอนกำลังหรือชนะแล้วกลับแคมเปญ รางวัลไม่ซ้ำ casualty คงอยู่
- Save, เปลี่ยน state, Load กลับมาได้; ข้อมูลเสียถูกปฏิเสธโดยไม่ทับ state ปัจจุบัน ทดสอบ backup แยก
- ชนะจังหวัดศัตรูสุดท้าย หรือสูญเสียหน่วยทั้งหมด เห็นผลและปุ่มเริ่มใหม่
- build แล้วเปิด executable นอก Editor ทำ checklist หลักซ้ำ

## 10. Build และแก้ปัญหา

**WW3 War → Build Windows** เรียก smoke checks ก่อน build หรือใช้ [Build-Windows.ps1](../tools/Build-Windows.ps1):

```powershell
./tools/Build-Windows.ps1 -UnityPath 'D:\YourUnityFolder\Editor\Unity.exe'
```

ปิด Editor ของโปรเจกต์ก่อน batch build คาดหวัง `Builds/Windows/ww3 war.exe` และไฟล์ runtime ข้างเคียง Log อยู่ `Logs/build-windows.log` ต้องแจกทั้งโฟลเดอร์

- **ไม่พบ Editor:** ตรวจ Hub → Installs; มี Hub/CLI อย่างเดียวไม่พอ ส่ง path ของ Editor จริง ไม่ต้องแก้เวอร์ชันโปรเจกต์แบบสุ่ม
- **ฉากว่างก่อน Play:** เป็นไปตามการออกแบบ runtime bootstrap
- **ไม่มีภาพสนาม:** ตรวจ compile error, shader, pipeline และว่า UI backdrop ไม่วาดทับใน Battle
- **คุมกล้องไม่ได้:** ใช้ legacy input (`activeInputHandler: 0`); หน้าจอต้องอยู่ Battle และ Game view ต้องมี focus
- **รับหน่วยไม่ได้:** ตรวจเงิน actions จำนวนหน่วย สถานะ pending และผลแคมเปญ
- **REA native ไม่พร้อม:** อ่าน [รายงาน](reverse-engineering/REPORT.md); ไม่ใช่เหตุให้แกล้งรายงานว่า decompile สำเร็จ หรือแก้ DRM เกมต้นฉบับ
