# Verification / ผลตรวจส่งมอบ

Date / วันที่: **2026-10-12**

## Passed / ผ่าน

| Check | Result / ผล |
| --- | --- |
| .NET core build | `dotnet build Tests/WW3War.CoreChecks/WW3War.CoreChecks.csproj --configuration Release --no-restore`: succeeded, **0 warnings, 0 errors** |
| Core regression executable | **20 checks passed**, including 100 seeded campaigns with up to 30 operations each, legal/illegal actions, economy, supply attrition, deterministic battle rosters, isolated snapshots, casualties, duplicate/forged battle results, corrupt save states, field-based JSON and damage |
| Actual linked code | [Check project](../Tests/WW3War.CoreChecks/WW3War.CoreChecks.csproj) compiles the real [Core sources](../Assets/WW3War/Scripts/Core/CampaignRules.cs); no Unity API stubs |
| PowerShell syntax | Both [Open-Unity.ps1](../tools/Open-Unity.ps1) and [Build-Windows.ps1](../tools/Build-Windows.ps1) parsed with the installed Windows PowerShell parser without errors |
| Read-only REA PE inspection | Both binaries succeeded; evidence SHA-256 values independently matched the currently installed files |
| Supplementary local inspection | Completed: **30 packs inventoried, 12 manual pages inspected**. This is custom Python, not REA decompilation |
| Documentation | [Thai](DEVELOPMENT.th.md), [English](DEVELOPMENT.en.md), [README](../README.md) and [REA report](reverse-engineering/REPORT.md) are UTF-8 Markdown; relative file links checked |
| Static source review | Replaced the `Screen` enum name that shadowed `UnityEngine.Screen`; the opaque backdrop is conditional outside Battle; selected-unit state is cleared before battle units are cleared. This is source review, **not** Unity compilation |
| Working-copy whitespace | `git diff --check` passed for tracked modifications; Git warned only about expected LF/CRLF conversion |

Local execution logs: [core checks](../Logs/core-checks-final.log), [Unity build attempt](../Logs/unity-build-attempt.log). Logs are ignored local artifacts, not portable source dependencies. REA command receipts are preserved in [commands.json](reverse-engineering/evidence/current/commands.json).

## Blocked or unavailable / ยังตรวจไม่ได้

- **Unity Windows build: failed before Editor launch.** The launcher returned exit code 1 because **Unity Editor 6000.0.84f1 was not found in standard locations**. Inspection found Unity Hub and a standalone Unity CLI, neither of which is the Editor. No Editor installation was attempted and no Windows player executable was produced by this task.
- Consequently **Unity project compilation/import, actual `JsonUtility`, Editor smoke checks, shader compilation/rendering, Play Mode input/UI, save replacement/backup in a Unity player, and the built executable user flow remain unverified**. The standalone .NET serialization check uses `System.Text.Json`, not Unity JSON.
- **REA native analysis: failed**, exit code 1, `provider_unavailable`. Ghidra is unconfigured, IDA is unregistered, Hopper does not support this Windows host. Java 21 is available. No native decompilation or runtime gameplay capture was obtained.
- File-change hooks are unavailable in this SDK host; explicit commands above were used instead.
- No FPS, scaling benchmark or performance improvement is claimed. No native Unity browser preview was fabricated.

## Next validation in the user's actual interface / ขั้นตรวจใน Unity

Install the pinned Editor, open the project and run **WW3 War → Run Smoke Checks**. Then complete the Play Mode/player checklists in the [Thai guide](DEVELOPMENT.th.md#9-การทดสอบ) or [English guide](DEVELOPMENT.en.md#9-testing), including campaign → battle → report → save/load, two resolutions, pause and withdrawal. Finally run **WW3 War → Build Windows** and launch the complete Windows output outside the Editor.

## Workspace ownership / การรักษางานเดิม

Existing untracked runtime/tool files were extended, not removed. Additional combined-guide/findings Markdown files appeared during this task and were left untouched; the authoritative guides for this delivery are the two separate language guides linked above. No staging, commit, push, proprietary asset extraction or vendor installation modification was performed.
