# ww3 war

เกมต้นแบบสงครามสมัยใหม่ใน Unity: แคมเปญผลัดตา + สนามรบเรียลไทม์ เขียนใหม่และสร้างโมเดลด้วยโค้ด ไม่ใช้ทรัพยากรจาก Total War

An independently implemented modern-war Unity prototype combining a turn-based campaign with real-time ground battles. All battlefield geometry is generated in code; no Total War assets are required.

> **Status / สถานะ:** Core C# regression checks pass. **Unity Editor was not found in the inspected local install locations**, so Editor import, Play Mode, shader rendering and Windows player build are **not verified**. Unity Hub and the standalone Unity CLI are not the Unity Editor. This is a small prototype, not a finished Total War-scale game.

## Documentation / เอกสาร

- [คู่มือพัฒนา Unity ภาษาไทย](docs/DEVELOPMENT.th.md)
- [Unity development guide — English](docs/DEVELOPMENT.en.md)
- [คู่มือใช้โมเดล 3D แบบละเอียด ไทย–อังกฤษ / Generated 3D model manual](docs/3D-MODELS-TH-EN.md)
- [รายงาน REA และขอบเขตหลักฐาน / Local REA report](docs/reverse-engineering/REPORT.md)
- [ผลตรวจส่งมอบ / Verification record](docs/VERIFICATION.md)

In Unity, open **WW3 War → Documentation** to access the Markdown guides through the system's default application. The UI in this prototype is English; the documentation is bilingual.

## Open in Unity / เปิดใน Unity

1. Install **Unity 6000.0.84f1** with **Windows Build Support (Mono)** using Unity Hub. No Unity Editor installation was performed by this task.
2. Unity Hub → Add project from disk → select this directory (the directory containing `Assets`, `Packages` and `ProjectSettings`).
3. Open **WW3 War → Open Main Scene**, then press **Play**. Runtime initialization constructs the game; an otherwise empty scene is intentional.
4. Run **WW3 War → Run Smoke Checks** and inspect the Console. For a player build use **WW3 War → Build Windows**.

Optional PowerShell launch/build with an actual Editor executable:

```powershell
./tools/Open-Unity.ps1 -UnityPath 'D:\YourUnityFolder\Editor\Unity.exe'
./tools/Build-Windows.ps1 -UnityPath 'D:\YourUnityFolder\Editor\Unity.exe'
```

Expected output: `Builds/Windows/ww3 war.exe`. Distribute the **whole** `Builds/Windows` directory, not just the executable. Do not pass the CLI executable under `AppData\Local\Unity\bin` as an Editor path.

## Prototype features / ระบบต้นแบบ

- Ten connected fictional provinces; credits, income, upkeep, supply and enemy expansion.
- Recruit mechanized infantry, main battle armor and mobile artillery; one player army, maximum 12 formations.
- Select, move, hold, automatic ranged fire, armor mitigation, cover mitigation, camera pan/rotate/zoom, pause and 1×/2× speed.
- Casualties persist into the campaign; withdrawal, defeat and victory; validated local JSON save/load with previous-save backup.
- A battle result is committed once using its battle ID. No networking, telemetry, accounts or service keys.

### Controls / ควบคุม

| Input | Action / การทำงาน |
| --- | --- |
| Campaign province button | Select province / เลือกจังหวัด |
| Move / Launch Operation | Move to friendly neighbor or attack nonfriendly neighbor / ย้ายหรือโจมตีจังหวัดที่เชื่อมกัน |
| Reinforcement / End Turn | Recruit or settle income/upkeep/supply / รับกำลังเสริมและจบตา |
| LMB on a blue formation, roster card, or `1`–`9` | Select formation / เลือกหน่วย (cards cover all 12) |
| RMB on battlefield | Move selected formation / สั่งเดิน |
| `F` | Hold position / หยุดเดิน |
| `Space` or Pause button | Pause/resume battle simulation / หยุดหรือเล่นต่อ |
| Speed button | Toggle 1×/2× / เปลี่ยนความเร็ว |
| `W A S D`, `Q E`, mouse wheel | Pan, rotate, zoom / เลื่อน หมุน ซูมกล้อง |
| Withdraw | Retreat with living units; no territory reward / ถอนกำลังที่ยังรอด |

Capture every **hostile** province to win; neutral provinces need not all be captured. The central relay and warehouses are scenery, not capturable objectives or collision/pathfinding obstacles. Sandbag locations within 5 world units reduce incoming damage.

## Checks / การทดสอบ

Requires **.NET SDK 10** for the standalone checks, not for playing in Unity:

```powershell
dotnet run --project Tests/WW3War.CoreChecks/WW3War.CoreChecks.csproj --configuration Release
```

This runs 20 checks against the real core source files, including campaign actions, economy, casualties, battle transaction integrity, invalid saves and a multi-seed simulation. It does **not** test Unity input/rendering or Unity's `JsonUtility`. An actual Editor smoke-check entrypoint and a manual play/build checklist are provided in the guides.

## Limits / ข้อจำกัด

Single-player ground-combat prototype only: no diplomacy, technology tree, construction, multi-army enemy campaign, formations of thousands, morale/routing, fog of war, navmesh, aerial/naval warfare, multiplayer or audio. Balance is fictional arcade balance, not military simulation. Local REA PE inspection succeeded; native decompilation was blocked by unavailable providers. See the report rather than treating the prototype as a reconstruction of proprietary internals.
