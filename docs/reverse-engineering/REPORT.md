# ROME II → WW3 War: local analysis / รายงานวิเคราะห์ในเครื่อง

## Scope / ขอบเขต

**TH:** ศึกษาสำเนา ROME II ที่ติดตั้งในเครื่องแบบอ่านอย่างเดียว เพื่อออกแบบเกมใหม่ชื่อ **ww3 war** ใน Unity ไม่ใช่การกู้ source code หรือการโคลนเกมเต็ม ไม่แก้ executable, ไม่ข้าม DRM, ไม่แตกไฟล์ pack และไม่ใช้ภาพ เสียง โมเดล แผนที่ หรือโค้ดของผู้ผลิตในเกมใหม่

**EN:** Read-only inspection of the local ROME II installation to inform an independently implemented Unity prototype named **ww3 war**. This is not recovered source code or a full clone. No executable patches, DRM bypass, pack payload extraction, or reuse of vendor code, art, audio, models or maps.

## Local target and method / เป้าหมายและวิธีการ

- Installation: `D:\Stram\steamapps\common\Total War Rome II`
- Steam manifest: `D:\Stram\steamapps\appmanifest_214950.acf` (app ID 214950).
- REA: local `rea-agents` **6.4.0**, invoked with Node via its existing `scripts/rea.mjs`; no global installation or configuration changed.
- Exact commands, UTC time and exit codes: [commands.json](evidence/current/commands.json).
- Supplementary inspector: [inspect_local_install.py](../../tools/rea/inspect_local_install.py). It is **ordinary Python, not an REA provider**. Its records have `origin: custom-read-only-python-inspector`; REA records use their own evidence envelope.
- Current-run evidence lives in [evidence/current](evidence/current/commands.json). Older files directly under `evidence/` predate this run and are not new verification.

## Findings and confidence / ผลและระดับความเชื่อมั่น

| Observation / สิ่งที่พบ | Evidence / หลักฐาน | Interpretation / ข้อจำกัด |
| --- | --- | --- |
| REA PE-resource inspection succeeded for `Rome2.exe` and `empire.retail.dll`; PE32, machine 332 (`0x14c`, x86) | [EXE resources](evidence/current/rea-pe-resources-rome2.json), [DLL resources](evidence/current/rea-pe-resources-empire.json) | Static resource structure and hashes only; not game logic or runtime architecture reconstruction |
| Native deep analysis failed with `provider_unavailable` | [REA diagnostics](evidence/current/rea-doctor.json), [native attempt](evidence/current/rea-native-analysis.json) | Ghidra is not configured, IDA registration is absent, Hopper is unsupported on this Windows host. Java 21 is available. No decompiled functions or verified call graph were obtained |
| 30 `.pack` files inventoried; only first 32 bytes per pack read | [Inventory](evidence/current/local-install-inventory.json) | File names, sizes and header hashes; not full-file hashes or decoded member contents |
| PE sections, imports, CLI directory and bounded ASCII-string observations | [EXE inspection](evidence/current/local-pe-Rome2-exe.json), [DLL inspection](evidence/current/local-pe-empire-retail-dll.json) | Custom Python static observations. Imports are declarations; strings do not establish function ownership or execution |
| Input declarations include campaign/battle modes and action names | [Default inputs](evidence/current/local-input-declarations.json) | Declared defaults, not active player settings or runtime bindings |
| 12 installed manual HTML pages inspected, with hashed file identity and bounded text previews | [Manual observations](evidence/current/local-manual-observations.json) | Vendor documentation, not live gameplay measurement. Original complete pages remain in the installation |

## From observations to an original design / จากหลักฐานสู่การออกแบบใหม่

Manual identifiers below refer to file-name prefixes recorded in the manual evidence. **The design in the final column is our choice, not an inferred ROME II implementation.**

| Documented concept / แนวคิดจากคู่มือ | Source prefix | WW3 War implementation / การออกแบบใหม่ |
| --- | --- | --- |
| Turn-based campaign, real-time battlefield | `0012`, `0075` | Ten fictional connected provinces, 3 strategic actions per turn, a separate real-time scene state |
| Recruitment and recurring upkeep | `0017` | Mechanized infantry, armor and artillery; credits, cost and upkeep defined in our catalog |
| Replenishment and attrition | `0026` | Supply 0–100, recovery when funded/supplied, attrition during shortages |
| Provinces and settlements | `0044` | A simple graph with income and ownership, not the original map or building system |
| Formations and battlefield orders | `0081` | Select a formation, move with right-click, automatic ranged engagement, hold orders |
| Morale, deployment, diplomacy, research, construction | `0087`, `0077`, `0038`, `0039`, `0045` | Documented expansion opportunities, **not implemented** in this prototype |

All unit values, province names, RNG, combat formulas, geometry, UI, save schema and game rules are independent work. This is practical source separation, not a formal legal clean-room certification or a claim of behavioral equivalence.

## Deliverable criteria / เกณฑ์ส่งมอบ

1. Unity project retains `ProjectSettings/ProjectVersion.txt` at **6000.0.84f1** and the existing entry scene `Assets/WW3War/Scenes/Main.unity`.
2. Product name **ww3 war**, modern fictional ground warfare; playable campaign → operation → after-action report → campaign loop, recruitment, economy, supply, victory/defeat, save/load.
3. Runtime uses Unity's built-in rendering/IMGUI/input and independently generated geometry; no paid assets or external service credentials.
4. Player army cap **12 formations**; ten provinces; combat intentionally small-scale, not thousands of simulated soldiers. No performance equivalence or speedup claimed.
5. Thai and English Markdown development guides: [DEVELOPMENT.th.md](../DEVELOPMENT.th.md), [DEVELOPMENT.en.md](../DEVELOPMENT.en.md); root [README](../../README.md).
6. Reproducible core-rule checks run outside Unity. Actual Editor import, shader compilation, Play Mode and Windows build require an installed Editor and are reported separately, never inferred from .NET results.

## Reproduce / ทำซ้ำ

From the project root in PowerShell, substitute the actual local REA path:

```powershell
$rea = 'D:\งาน\clone app\rea-main\rea-main\rea-main copy\scripts\rea.mjs'
$game = 'D:\Stram\steamapps\common\Total War Rome II'
node $rea inspect-pe-resources "$game\Rome2.exe" --format json
node $rea inspect-pe-resources "$game\empire.retail.dll" --format json
node $rea doctor --provider ghidra --format json
node $rea analyze "$game\Rome2.exe" --provider ghidra --format json
python tools/rea/inspect_local_install.py --install $game --manifest 'D:\Stram\steamapps\appmanifest_214950.acf' --out Logs/local-analysis
```

The last two REA readiness/native-analysis checks are expected to fail until an appropriate provider is configured. The inspector writes only outside the installation. Do not overwrite the recorded evidence when comparing future runs; use a separate output directory. Native disassembly and live gameplay observation remain future work, not completed results.
