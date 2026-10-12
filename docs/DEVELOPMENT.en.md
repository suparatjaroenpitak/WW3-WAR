# Developing ww3 war in Unity

[ภาษาไทย](DEVELOPMENT.th.md) · [README](../README.md) · [Detailed 3D model manual](3D-MODELS-TH-EN.md) · [Local REA report](reverse-engineering/REPORT.md)

## 1. Purpose and status

**ww3 war** is an independently implemented modern-war prototype: a turn-based campaign linked to real-time battles in fictional provinces. It generates its own geometry and does not require ROME II to play. The Unity game does not contain the reference game's assets or recovered source.

The prototype includes recruitment, credits/upkeep, supply, operations, persistent casualties, victory/defeat and save/load. It is not a finished AAA game or a source reconstruction. Read the separate REA report for the precise observation boundaries.

**Current verification:** 20 standalone core C# checks pass. A Unity Editor was not found in the inspected installation locations, so Editor import, Play Mode, shader rendering and Windows player builds remain **unverified**. See [VERIFICATION](VERIFICATION.md).

## 2. Setup and first run

1. Install **Unity 6000.0.84f1** using Unity Hub. Add **Windows Build Support (Mono)** for Windows builds.
2. Add this project from disk, selecting the folder containing `Assets`, `Packages` and `ProjectSettings`, not just the Assets folder.
3. Let Unity finish importing and inspect the Console. Choose **WW3 War → Open Main Scene**.
4. Press **Play**. The runtime bootstrap creates the game, camera, lighting and UI; you do not need to attach components manually.
5. Choose **WW3 War → Documentation → Development Guide (English)** to open this Markdown document in the system's default application.

The target version is in [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt); dependencies are in [manifest.json](../Packages/manifest.json). The project uses the Built-in pipeline, IMGUI, legacy Input and existing Unity modules, not URP/HDRP or Asset Store packages.

Optional PowerShell launcher, from the project root:

```powershell
./tools/Open-Unity.ps1 -UnityPath 'D:\YourUnityFolder\Editor\Unity.exe'
```

[Open-Unity.ps1](../tools/Open-Unity.ps1) does not install or update anything. Unity Hub and the Unity CLI are not the Editor, even if a CLI executable is also named `Unity.exe`.

## 3. Source map and responsibility boundaries

| File | Responsibility |
| --- | --- |
| [Main.unity](../Assets/WW3War/Scenes/Main.unity) | Lightweight entry scene; runtime constructs the game |
| [CampaignModels.cs](../Assets/WW3War/Scripts/Core/CampaignModels.cs) | Serializable public-field DTOs for Unity JSON |
| [CampaignRules.cs](../Assets/WW3War/Scripts/Core/CampaignRules.cs) | Campaign actions, RNG, validation, battle commitment; no Unity dependency |
| [UnitCatalog.cs](../Assets/WW3War/Scripts/Core/UnitCatalog.cs) | Original arcade unit prices, upkeep and statistics |
| [CombatRules.cs](../Assets/WW3War/Scripts/Core/CombatRules.cs) | Armor/cover damage calculation; no Unity dependency |
| [WW3WarGame.cs](../Assets/WW3War/Scripts/Runtime/WW3WarGame.cs) | Screen flow, input, simulation, IMGUI and save/load |
| [ProceduralFactory.cs](../Assets/WW3War/Scripts/Runtime/ProceduralFactory.cs) | Primitive-based soldiers, vehicles, roads, sandbags and scenery |
| [RTSCamera.cs](../Assets/WW3War/Scripts/Runtime/RTSCamera.cs) | Pan, rotation and zoom |
| [FieldSurface.shader](../Assets/WW3War/Resources/FieldSurface.shader) | Simple Built-in color/lighting shader |
| [BuildCommands.cs](../Assets/WW3War/Editor/BuildCommands.cs) | Scene, documentation and Windows-build menu commands |
| [SmokeChecks.cs](../Assets/WW3War/Editor/SmokeChecks.cs) | Editor asset checks, Unity JSON round trips and battle transaction checks |
| [Program.cs](../Tests/WW3War.CoreChecks/Program.cs) | Standalone regression executable linking the real core sources |

Keep `Core` pure C# so it remains testable outside Unity. `Runtime` owns Unity objects, time, input and filesystem access. Put reusable economic rules in the core rather than GUI buttons, particularly before adding AI or multiplayer.

## 4. Campaign rules and interfaces

A new expedition starts at Aster Harbor on turn 1 with **420 credits, 85 supply, 3 actions**, and one formation of each unit type. There are ten provinces and one player army, capped at **12 formations**. Province ownership is `0 = neutral`, `1 = player`, `2 = enemy`.

- Move only to a connected friendly neighbor: **1 action, 3 supply**.
- Attack a connected nonfriendly neighbor: **1 action, 8 supply**, paid when the battle is committed.
- Recruit in a friendly province: pay the unit cost and **1 action**.
- End turn: collect friendly-province income and deduct unit upkeep; supply changes by `18 - army count × 2`, clamped to 0–100.
- If available credits plus income cannot pay upkeep, or supply plus 18 cannot cover demand, formations lose **18 percentage points** of health. Otherwise they regain **8 percentage points**, capped at 100%. Remove formations at or below zero health.
- On even turns the enemy occupies one adjacent candidate province, excluding the player's army location. This is a simplified ownership rule, not a simulated moving enemy campaign army.
- Win when no hostile provinces remain while the player still has an army and friendly territory. Neutral provinces need not all be captured. Lose when the army or friendly territory is gone.

Public interfaces:

```csharp
CampaignState state = CampaignRules.NewCampaign(2026);
CampaignRules.MoveArmy(state, "vale", out string moveMessage);
CampaignRules.Recruit(state, UnitKind.Infantry, out string recruitMessage);
string turnMessage = CampaignRules.EndTurn(state);
bool ok = CampaignRules.Validate(state, out string validationMessage);
```

Always inspect boolean results/messages. Validate deserialized DTOs before use. Validation enforces structure and consistency; it is not a cryptographic anti-cheat guarantee against edited local saves.

## 5. Battle transaction boundary

`TryBeginAttack` records `PendingBattle` and returns a separate `BattleSpec.Copy()`. The record includes the battle ID, deterministic seed, origin/target and committed rosters. Strategic movement, recruitment and end-turn actions are blocked while a battle is pending.

```csharp
if (CampaignRules.TryBeginAttack(state, "ridge", out BattleSpec battle, out string message))
{
    // Pass battle to the runtime; retain formation Id/Kind.
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

This illustrates API use, not an implemented auto-resolve feature. Survivors must be distinct members of the committed roster with unchanged kinds and no health gain. Mismatched battle IDs, duplicate IDs and invalid health are rejected. Commitment clears `PendingBattle`, preventing repeated rewards. Victory captures the target, moves the army and grants **45 credits**. Withdrawal submits a loss with the living formations, without a territory reward.

## 6. Tactical behavior and controls

Select a connected target and launch an operation. Select blue units with a world left-click, roster card or keys **1–9**; cards reach all twelve formations. Right-click the exposed battlefield to move. **F** holds the selected formation. Units automatically fire at their nearest living enemy in range.

**WASD** pans, **Q/E** rotates, and the wheel zooms. **Space** or Pause freezes the simulation while leaving the camera usable. Speed switches between **1× and 2×**. After-action duration is simulated time. Withdraw retreats immediately with survivors.

| Formation | Cost | Upkeep | HP | Speed | Range | Damage | Reload | Armor |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Mechanized Infantry | 90 | 6 | 110 | 4.8 | 24 | 13 | 1.2 | 0.05 |
| Main Battle Armor | 150 | 12 | 230 | 6.2 | 32 | 35 | 2.5 | 0.30 |
| Mobile Artillery | 170 | 10 | 90 | 3.7 | 54 | 51 | 4.5 | 0 |

These are fictional world-unit arcade values, not real equipment measurements. Shot damage is `attacker damage × (1 - defender armor) × cover multiplier`. The cover multiplier is **0.65** when the target is less than **5 world units** from a sandbag cover point. Both sides use this rule. Artillery currently deals direct damage, without ballistics or splash.

**Intentional limits:** straight-line movement, no navmesh/collision/avoidance/line-of-sight. Warehouses are scenery. The central relay is not a capturable objective. There is no morale/routing, deployment phase, audio, fog of war or multiplayer. Do not present these as completed features.

## 7. Save schema and filesystem behavior

Save Campaign writes `ww3-war-campaign.json` under `Application.persistentDataPath`. On Windows this is normally `%USERPROFILE%\AppData\LocalLow\WW3 War Studio\ww3 war`; inspect the actual Unity value if the machine differs.

- The JSON root is a `CampaignState`, with **no version envelope yet**.
- Important fields: `Turn`, `Credits`, `Supply`, `Actions`, `ArmyProvinceId`, `Provinces`, `Army`, `Seed`, `Victory`, `Defeat`, `BattleSequence`, `RecruitmentSequence`, `PendingBattle`.
- `FormationState.Health` is a fraction in `(0,1]`, not raw HP. `Kind` uses numeric enum values 0/1/2.
- Save writes `.tmp` first, then replaces the original while retaining a `.bak`; an initial save moves the temporary file into place. Failures are shown in the UI notice.
- Load rejects files over **1 MiB** and calls `CampaignRules.Validate` before replacing the current campaign. It does not automatically restore `.bak`.
- The UI does not save mid-battle. A loaded valid pending battle restarts from its committed roster; tactical positions/time are not saved or resumed.

Before changing the schema, introduce explicit versioning and migrations. Test old, incomplete, malformed and duplicate data. Never bypass validation to feed corrupt state into a battle.

## 8. Extending the prototype

### New unit type

Extend the enum/validation, catalog, procedural model, enemy generator (currently assumes three kinds), recruitment UI and save rules together. Add regression checks. Preserve old enum numeric values, because saves store them as numbers.

### New province

Update `CreateMap` with unique IDs, normalized 0–1 coordinates and symmetric neighbor links. Update the validator's ten-province expectation and its tests. Use original map data rather than copying the reference game's geography.

### Visual and performance work

Replace primitive models with original or properly licensed prefabs. Separate presentation/selection from simulation before scaling up. Primitive colliders are intentionally disabled, so pathfinding requires a separate navigation representation. Profile a representative workload before choosing pooling, instancing or ECS. This delivery includes no FPS measurement or claimed performance improvement.

### Suggested milestones

1. Complete Editor/player validation and tune UI for target resolutions.
2. Multi-selection and formation orders, health bars, minimap and tutorial.
3. Objective capture, suppression/morale and mid-battle persistence.
4. Technology, diplomacy, construction and multiple campaign armies, with independently testable state.

## 9. Testing

### Standalone core verification

Requires **.NET SDK 10**, from the project root:

```powershell
dotnet run --project Tests/WW3War.CoreChecks/WW3War.CoreChecks.csproj --configuration Release
```

The [check project](../Tests/WW3War.CoreChecks/WW3War.CoreChecks.csproj) links the real core source files. It has no fake Unity API or third-party test packages. Twenty checks cover movement, recruitment, upkeep, supply, attrition, deterministic enemies, battle locking/commitment, corrupt saves, damage and multi-seed campaigns. Its JSON round trip uses `System.Text.Json`; that is **not** verification of Unity's `JsonUtility`.

### Actual Editor checks

Use **WW3 War → Run Smoke Checks** for scene/shader asset presence, `JsonUtility` round trips and battle transactions. Alternatively:

```powershell
& 'D:\YourUnityFolder\Editor\Unity.exe' -batchmode -nographics -quit -projectPath (Get-Location).Path -executeMethod WW3War.Editor.SmokeChecks.Run -logFile Logs/editor-smoke.log
```

Inspect the real process exit code and log. These checks do not prove rendered shader correctness or input behavior.

### Required Play Mode/player acceptance checklist

- No compile errors; new campaign shows all ten provinces and a usable scrolling sidebar at 1280×720 and 1600×900.
- Recruitment, movement and end turn update credits, supply, actions and roster correctly.
- Launching an operation shows actual geometry, not a battlefield hidden by a full-screen UI backdrop.
- World/card/key selection, movement, hold, camera, pause/resume and 2× speed work; both sides damage and destroy formations.
- Compare cover and open-ground damage. Withdraw or win, return to campaign, verify casualties persist and rewards do not repeat.
- Save, mutate state, load and verify restoration. Corrupt data must be rejected without replacing the live campaign; check backup behavior separately.
- Capture the last hostile province or lose every formation; check outcome and new-campaign controls.
- Build and launch the player outside the Editor; repeat the main user flow.

## 10. Windows builds and troubleshooting

**WW3 War → Build Windows** runs Editor smoke checks before building. Or use [Build-Windows.ps1](../tools/Build-Windows.ps1):

```powershell
./tools/Build-Windows.ps1 -UnityPath 'D:\YourUnityFolder\Editor\Unity.exe'
```

Close this project in the interactive Editor before batch building. Expected executable: `Builds/Windows/ww3 war.exe`; log: `Logs/build-windows.log`. Distribute the **entire output folder**, including data/runtime files.

- **Editor missing:** check Hub → Installs. Hub/CLI alone cannot compile this project. Supply a real Editor path rather than randomly changing the target version.
- **Empty scene before Play:** expected; the runtime bootstrap creates the game.
- **No battlefield:** inspect Console errors, shader/pipeline selection and Battle backdrop behavior.
- **Camera input inactive:** the project uses legacy input (`activeInputHandler: 0`); be in Battle with Game view focused.
- **Recruitment blocked:** inspect funds, actions, army cap, pending operation and campaign outcome.
- **REA native unavailable:** consult the [analysis report](reverse-engineering/REPORT.md). This is not successful decompilation and does not justify bypassing the original game's DRM.
