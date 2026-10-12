using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WW3War.Core;

namespace WW3War
{
    /// <summary>Playable, asset-free campaign and real-time battle prototype.</summary>
    public sealed class WW3WarGame : MonoBehaviour
    {
        enum GameScreen { Campaign, Battle, Results }

        sealed class BattleUnit
        {
            public FormationState Source;
            public int Team;
            public float Health;
            public float Cooldown;
            public Vector3 Destination;
            public GameObject Root;
            public UnitStats Stats;
        }

        const string SaveName = "ww3-war-campaign.json";
        static readonly Color Background = new Color(.045f, .065f, .075f);
        static readonly Color Panel = new Color(.085f, .12f, .13f);
        static readonly Color Accent = new Color(.91f, .68f, .29f);
        static readonly Color Muted = new Color(.62f, .69f, .67f);

        CampaignState campaign;
        GameScreen screen;
        BattleSpec activeBattle;
        BattleResolution lastResult;
        readonly List<BattleUnit> battleUnits = new List<BattleUnit>();
        GameObject battlefield;
        GameObject selectionRing;
        readonly List<Vector3> coverPositions = new List<Vector3>();
        bool battlePaused;
        float battleSpeed = 1f;
        Vector2 sidebarScroll;
        RTSCamera rtsCamera;
        ProvinceState selectedProvince;
        int selectedUnit = -1;
        float battleElapsed;
        string notice = "Choose a connected province to move or attack.";
        string resultTitle = "";
        string resultBody = "";
        GUIStyle titleStyle, headingStyle, bodyStyle, smallStyle, buttonStyle, selectedButtonStyle;
        Texture2D pixel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<WW3WarGame>() == null)
                new GameObject("WW3 WAR — Game Runtime").AddComponent<WW3WarGame>();
        }

        void Start()
        {
            Application.targetFrameRate = 60;
            EnsureCameraAndLight();
            campaign = CampaignRules.NewCampaign();
            selectedProvince = CampaignRules.GetProvince(campaign, campaign.ArmyProvinceId);
            screen = GameScreen.Campaign;
            notice = "Secure the theater. Capture every opposing province to win.";
        }

        void EnsureCameraAndLight()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.07f, .1f, .11f);
            camera.fieldOfView = 48;
            rtsCamera = camera.GetComponent<RTSCamera>();
            if (rtsCamera == null) rtsCamera = camera.gameObject.AddComponent<RTSCamera>();
            rtsCamera.InputEnabled = false;
            var light = FindFirstObjectByType<Light>();
            if (light == null)
            {
                var lightObject = new GameObject("Field Sun");
                light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                lightObject.transform.rotation = Quaternion.Euler(48, -35, 0);
            }
            RenderSettings.ambientLight = new Color(.55f, .57f, .52f);
        }

        void Update()
        {
            if (screen != GameScreen.Battle) return;
            HandleBattleInput();
            if (battlePaused) return;
            float deltaTime = Time.deltaTime * battleSpeed;
            battleElapsed += deltaTime;
            TickBattle(deltaTime);
        }

        void HandleBattleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space)) battlePaused = !battlePaused;
            if (Input.GetKeyDown(KeyCode.F) && selectedUnit >= 0)
                battleUnits[selectedUnit].Destination = battleUnits[selectedUnit].Root.transform.position;
            int slot = 0;
            for (int i = 0; i < battleUnits.Count && slot < 9; i++)
            {
                if (battleUnits[i].Team != 1 || battleUnits[i].Health <= 0) continue;
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + slot))) SelectPlayerUnit(i);
                slot++;
            }

            // Keep clicks on the header and roster from issuing world orders.
            bool overField = Input.mousePosition.y > 118 && Input.mousePosition.y < Screen.height - 202;
            if (overField && Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Plane ground = new Plane(Vector3.up, Vector3.zero);
                if (ground.Raycast(ray, out float hitDistance))
                {
                    Vector3 point = ray.GetPoint(hitDistance);
                    float nearest = 6f;
                    for (int i = 0; i < battleUnits.Count; i++)
                    {
                        if (battleUnits[i].Team != 1 || battleUnits[i].Health <= 0) continue;
                        float distance = Vector3.Distance(point, battleUnits[i].Root.transform.position);
                        if (distance < nearest) { nearest = distance; SelectPlayerUnit(i); }
                    }
                }
            }

            if (overField && Input.GetMouseButtonDown(1))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Plane ground = new Plane(Vector3.up, Vector3.zero);
                float distance;
                if (ground.Raycast(ray, out distance) && selectedUnit >= 0)
                {
                    Vector3 point = ray.GetPoint(distance);
                    point.x = Mathf.Clamp(point.x, -70, 70);
                    point.z = Mathf.Clamp(point.z, -48, 48);
                    battleUnits[selectedUnit].Destination = point;
                }
            }
        }

        void TickBattle(float deltaTime)
        {
            for (int i = battleUnits.Count - 1; i >= 0; i--)
            {
                BattleUnit unit = battleUnits[i];
                if (unit.Health <= 0)
                {
                    if (selectedUnit == i) selectedUnit = -1;
                    else if (selectedUnit > i) selectedUnit--;
                    Destroy(unit.Root);
                    battleUnits.RemoveAt(i);
                    continue;
                }

                unit.Cooldown -= deltaTime;
                BattleUnit target = FindNearestEnemy(unit, out float range);
                if (unit.Team == 2 && target != null && range > unit.Stats.Range * .72f)
                    unit.Destination = target.Root.transform.position;
                Vector3 current = unit.Root.transform.position;
                Vector3 destination = unit.Destination;
                destination.y = 0;
                if (Vector3.Distance(current, destination) > .6f)
                {
                    Vector3 direction = destination - current;
                    unit.Root.transform.position = Vector3.MoveTowards(current, destination, unit.Stats.Speed * deltaTime);
                    if (direction.sqrMagnitude > .01f) unit.Root.transform.rotation = Quaternion.Slerp(unit.Root.transform.rotation, Quaternion.LookRotation(direction), deltaTime * 2.5f);
                }
                if (target != null && range <= unit.Stats.Range && unit.Cooldown <= 0)
                {
                    bool inCover = false;
                    foreach (Vector3 cover in coverPositions)
                        if (Vector3.Distance(target.Root.transform.position, cover) < 5f) { inCover = true; break; }
                    target.Health -= CombatRules.ShotDamage(unit.Source.Kind, target.Source.Kind, inCover);
                    unit.Cooldown = unit.Stats.Reload;
                    DrawTracer(unit, target);
                }
            }

            int playerCount = CountTeam(1), enemyCount = CountTeam(2);
            if (enemyCount == 0) FinishBattle(true);
            else if (playerCount == 0) FinishBattle(false);
        }

        void DrawTracer(BattleUnit from, BattleUnit to)
        {
            Vector3 start = from.Root.transform.position + Vector3.up * 1.5f;
            Vector3 end = to.Root.transform.position + Vector3.up;
            var tracer = ProceduralFactory.Box(battlefield.transform, "Shot tracer", (start + end) * .5f,
                new Vector3(.09f, .09f, Vector3.Distance(start, end)),
                from.Team == 1 ? ProceduralFactory.Blue : ProceduralFactory.Red);
            tracer.transform.rotation = Quaternion.LookRotation(end - start);
            Destroy(tracer, .09f);
        }

        BattleUnit FindNearestEnemy(BattleUnit from, out float distance)
        {
            BattleUnit nearest = null;
            distance = float.MaxValue;
            Vector3 origin = from.Root.transform.position;
            foreach (BattleUnit other in battleUnits)
            {
                if (other.Team == from.Team || other.Health <= 0) continue;
                float candidate = Vector3.Distance(origin, other.Root.transform.position);
                if (candidate < distance) { distance = candidate; nearest = other; }
            }
            return nearest;
        }

        int CountTeam(int team)
        {
            int count = 0;
            foreach (BattleUnit unit in battleUnits) if (unit.Team == team && unit.Health > 0) count++;
            return count;
        }

        void BeginBattle(BattleSpec battle)
        {
            activeBattle = battle;
            battleElapsed = 0;
            battlePaused = false;
            battleSpeed = 1f;
            screen = GameScreen.Battle;
            selectedUnit = -1;
            if (battlefield != null) Destroy(battlefield);
            battlefield = new GameObject("Procedural Modern Battlefield");
            coverPositions.Clear();
            ProceduralFactory.Terrain(battlefield.transform, coverPositions);
            selectionRing = ProceduralFactory.Ring(battlefield.transform, ProceduralFactory.Blue, 3.5f);
            selectionRing.SetActive(false);
            battleUnits.Clear();
            SpawnSide(battle.PlayerUnits, 1, -34);
            SpawnSide(battle.EnemyUnits, 2, 34);
            if (rtsCamera != null)
            {
                rtsCamera.ResetView();
                rtsCamera.InputEnabled = true;
            }
            SelectPlayerUnit(0);
            notice = "Right-click to move a selected formation. Units engage enemies in range.";
        }

        void SpawnSide(List<FormationState> roster, int team, float z)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                float x = (i - (roster.Count - 1) * .5f) * 9f;
                var root = new GameObject((team == 1 ? "Blue " : "Red ") + roster[i].Id);
                root.transform.SetParent(battlefield.transform, false);
                root.transform.position = new Vector3(x, 0, z + (team == 1 ? 1 : -1) * (i % 2) * 5f);
                root.transform.rotation = Quaternion.Euler(0, team == 1 ? 0 : 180, 0);
                ProceduralFactory.UnitModel(root.transform, roster[i].Kind, team);
                UnitStats stats = UnitCatalog.Get(roster[i].Kind);
                var unit = new BattleUnit
                {
                    Source = roster[i].Copy(), Team = team, Stats = stats,
                    Health = stats.MaxHealth * roster[i].Health,
                    Destination = root.transform.position, Root = root,
                    Cooldown = (i % 3) * .4f
                };
                battleUnits.Add(unit);
            }
        }

        void FinishBattle(bool playerWon)
        {
            if (screen != GameScreen.Battle) return;
            lastResult = new BattleResolution
            {
                BattleId = activeBattle.BattleId,
                ProvinceId = activeBattle.ProvinceId,
                PlayerWon = playerWon,
                DurationSeconds = battleElapsed
            };
            foreach (BattleUnit unit in battleUnits)
                if (unit.Team == 1 && unit.Health > 0)
                    lastResult.Survivors.Add(new FormationState(unit.Source.Id, unit.Source.Kind,
                        Mathf.Clamp01(unit.Health / unit.Stats.MaxHealth)));

            if (battlefield != null) Destroy(battlefield);
            battlefield = null;
            selectionRing = null;
            selectedUnit = -1;
            battleUnits.Clear();
            if (rtsCamera != null) rtsCamera.InputEnabled = false;
            string message;
            if (!CampaignRules.ResolveBattle(campaign, lastResult, out message))
                message = "Battle result could not be committed. " + message;
            resultTitle = playerWon ? "OBJECTIVE SECURED" : "OPERATION FAILED";
            resultBody = message + "\nSurviving formations: " + lastResult.Survivors.Count
                + "\nSimulated duration: " + Mathf.FloorToInt(lastResult.DurationSeconds) + " seconds";
            notice = message;
            screen = GameScreen.Results;
            selectedProvince = CampaignRules.GetProvince(campaign, campaign.ArmyProvinceId);
        }

        void SelectPlayerUnit(int index)
        {
            if (index < 0 || index >= battleUnits.Count || battleUnits[index].Team != 1 || battleUnits[index].Health <= 0) return;
            selectedUnit = index;
        }

        void OnGUI()
        {
            EnsureStyles();
            if (screen != GameScreen.Battle) DrawBackdrop();
            if (selectionRing != null)
            {
                selectionRing.SetActive(selectedUnit >= 0);
                if (selectedUnit >= 0) selectionRing.transform.position = battleUnits[selectedUnit].Root.transform.position;
            }
            DrawHeader();
            if (screen == GameScreen.Campaign) DrawCampaign();
            else if (screen == GameScreen.Battle) DrawBattle();
            else DrawResults();
        }

        void EnsureStyles()
        {
            if (pixel != null) return;
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headingStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, wordWrap = true, fixedHeight = 34 };
            selectedButtonStyle = new GUIStyle(buttonStyle) { fontStyle = FontStyle.Bold };
        }

        void DrawBackdrop()
        {
            GUI.color = Background;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), pixel);
            GUI.color = Color.white;
        }

        void DrawHeader()
        {
            GUI.color = Panel;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, 66), pixel);
            GUI.color = Color.white;
            GUI.color = Accent;
            GUI.Label(new Rect(24, 8, 260, 30), "WW3 WAR", titleStyle);
            GUI.color = Muted;
            GUI.Label(new Rect(26, 38, 600, 20), screen == GameScreen.Battle ? "REAL-TIME TACTICAL OPERATION" : "MODERN WARFARE — THEATER COMMAND", smallStyle);
            GUI.color = Color.white;
            if (campaign != null)
            {
                string status = "TURN " + campaign.Turn + "     CREDITS " + campaign.Credits + "     SUPPLY " + campaign.Supply + "     ACTIONS " + campaign.Actions;
                GUI.Label(new Rect(Screen.width - 510, 21, 490, 24), status, headingStyle);
            }
        }

        Rect MapRect { get { return new Rect(24, 88, Screen.width * .62f, Screen.height - 112); } }
        Rect SideRect { get { Rect map = MapRect; return new Rect(map.xMax + 16, 88, Screen.width - map.xMax - 40, Screen.height - 112); } }

        void DrawCampaign()
        {
            Rect map = MapRect;
            DrawPanel(map, "OPERATIONAL THEATER  /  SELECT A PROVINCE");
            DrawMap(map);
            DrawCampaignSidebar(SideRect);
            GUI.color = Accent;
            GUI.Label(new Rect(28, Screen.height - 23, Screen.width - 56, 20), notice, smallStyle);
            GUI.color = Color.white;
        }

        void DrawMap(Rect rect)
        {
            Rect inner = new Rect(rect.x + 30, rect.y + 48, rect.width - 60, rect.height - 72);
            GUI.color = new Color(.14f, .18f, .16f);
            GUI.DrawTexture(inner, pixel);
            GUI.color = new Color(.22f, .27f, .24f);
            for (int i = 1; i < 8; i++) GUI.DrawTexture(new Rect(inner.x + inner.width * i / 8, inner.y, 1, inner.height), pixel);
            for (int i = 1; i < 6; i++) GUI.DrawTexture(new Rect(inner.x, inner.y + inner.height * i / 6, inner.width, 1), pixel);
            GUI.color = Color.white;
            var points = new Dictionary<string, Vector2>();
            foreach (ProvinceState province in campaign.Provinces)
                points[province.Id] = new Vector2(inner.x + province.MapX * inner.width, inner.y + (1 - province.MapY) * inner.height);

            foreach (ProvinceState province in campaign.Provinces)
                foreach (string neighbor in province.Neighbors)
                    if (string.CompareOrdinal(province.Id, neighbor) < 0) DrawLine(points[province.Id], points[neighbor], new Color(.66f, .68f, .57f, .65f));

            foreach (ProvinceState province in campaign.Provinces)
            {
                Vector2 point = points[province.Id];
                bool isSelected = selectedProvince != null && selectedProvince.Id == province.Id;
                bool isArmy = campaign.ArmyProvinceId == province.Id;
                Rect marker = new Rect(point.x - 48, point.y - 18, 96, 36);
                GUIStyle style = new GUIStyle(isSelected ? selectedButtonStyle : buttonStyle);
                style.normal.textColor = province.Owner == 1 ? new Color(.65f, .88f, .83f) : province.Owner == 2 ? new Color(1f, .66f, .57f) : new Color(.89f, .86f, .7f);
                string prefix = isArmy ? "◆ " : "";
                if (GUI.Button(marker, prefix + province.Name, style)) selectedProvince = province;
            }
            GUI.color = Muted;
            GUI.Label(new Rect(inner.x + 8, inner.y + 8, 180, 18), "◆ YOUR FIELD ARMY", smallStyle);
            GUI.color = Color.white;
        }

        void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            Vector2 delta = end - start;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, (start + end) * .5f);
            GUI.color = color;
            GUI.DrawTexture(new Rect((start.x + end.x) * .5f - delta.magnitude * .5f, (start.y + end.y) * .5f - 1, delta.magnitude, 2), pixel);
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        void DrawCampaignSidebar(Rect rect)
        {
            DrawPanel(rect, "COMMAND & LOGISTICS");
            var viewport = new Rect(rect.x + 10, rect.y + 44, rect.width - 20, rect.height - 54);
            float contentHeight = 470 + campaign.Army.Count * 19 + (campaign.Victory || campaign.Defeat ? 100 : 0);
            sidebarScroll = GUI.BeginScrollView(viewport, sidebarScroll, new Rect(0, 0, viewport.width - 20, contentHeight));
            float x = 6, y = 8, w = viewport.width - 32;
            ProvinceState province = selectedProvince;
            if (province != null)
            {
                GUI.color = Accent;
                GUI.Label(new Rect(x, y, w, 28), province.Name.ToUpperInvariant(), headingStyle);
                GUI.color = Color.white;
                string owner = province.Owner == 1 ? "FRIENDLY CONTROL" : province.Owner == 2 ? "HOSTILE CONTROL" : "CONTESTED / NEUTRAL";
                GUI.Label(new Rect(x, y + 28, w, 24), owner + "   ·   INCOME " + province.Income, bodyStyle);
                GUI.Label(new Rect(x, y + 54, w, 34), "Connections: " + string.Join(", ", province.Neighbors), smallStyle);
                y += 96;
                bool adjacent = CampaignRules.IsAdjacent(campaign, province.Id);
                bool canAct = campaign.Actions > 0 && campaign.PendingBattle == null && !campaign.Victory && !campaign.Defeat;
                if (province.Owner == 1 && province.Id != campaign.ArmyProvinceId)
                {
                    GUI.enabled = canAct && adjacent;
                    if (GUI.Button(new Rect(x, y, w, 36), "MOVE ARMY  ·  3 SUPPLY", buttonStyle))
                        RunCampaignAction(CampaignRules.MoveArmy(campaign, province.Id, out string message), message);
                    GUI.enabled = true;
                    y += 42;
                }
                else if (province.Owner != 1)
                {
                    GUI.enabled = canAct && adjacent;
                    if (GUI.Button(new Rect(x, y, w, 36), "LAUNCH OPERATION  ·  8 SUPPLY", buttonStyle))
                    {
                        if (CampaignRules.TryBeginAttack(campaign, province.Id, out BattleSpec battle, out string message))
                        {
                            notice = message;
                            BeginBattle(battle);
                        }
                        else notice = message;
                    }
                    GUI.enabled = true;
                    y += 42;
                }
            }

            GUI.color = Accent;
            GUI.Label(new Rect(x, y, w, 22), "FIELD ARMY  ·  " + campaign.Army.Count + "/" + CampaignRules.MaxArmySize, headingStyle);
            GUI.color = Color.white;
            y += 24;
            foreach (FormationState unit in campaign.Army)
            {
                GUI.Label(new Rect(x, y, w, 20), "• " + UnitCatalog.Get(unit.Kind).Name + "  —  " + Mathf.RoundToInt(unit.Health * 100) + "%", smallStyle);
                y += 19;
            }
            y += 8;
            GUI.Label(new Rect(x, y, w, 21), "REINFORCEMENTS", headingStyle);
            y += 24;
            float gap = 6, buttonWidth = (w - gap * 2) / 3;
            string[] recruit = { "INFANTRY\n90 CR", "ARMOR\n150 CR", "ARTILLERY\n170 CR" };
            for (int i = 0; i < 3; i++)
            {
                GUI.enabled = !campaign.Victory && !campaign.Defeat && campaign.Actions > 0 && campaign.Army.Count < CampaignRules.MaxArmySize && campaign.PendingBattle == null && campaign.Credits >= UnitCatalog.Get((UnitKind)i).Cost;
                if (GUI.Button(new Rect(x + i * (buttonWidth + gap), y, buttonWidth, 44), recruit[i], smallStyle))
                    RunCampaignAction(CampaignRules.Recruit(campaign, (UnitKind)i, out string message), message);
            }
            GUI.enabled = true;
            y += 52;
            GUI.enabled = campaign.PendingBattle == null && !campaign.Victory && !campaign.Defeat;
            if (GUI.Button(new Rect(x, y, w, 36), "END TURN  /  RESUPPLY", buttonStyle)) notice = CampaignRules.EndTurn(campaign);
            GUI.enabled = true;
            y += 42;
            float half = (w - 6) / 2;
            if (GUI.Button(new Rect(x, y, half, 30), "SAVE CAMPAIGN", smallStyle)) SaveCampaign();
            if (GUI.Button(new Rect(x + half + 6, y, half, 30), "LOAD CAMPAIGN", smallStyle)) LoadCampaign();
            y += 38;
            if (campaign.Victory || campaign.Defeat)
            {
                GUI.color = campaign.Victory ? new Color(.55f, .9f, .67f) : new Color(1f, .55f, .43f);
                GUI.Label(new Rect(x, y, w, 42), campaign.Victory ? "THEATER SECURED — VICTORY" : "EXPEDITION LOST — DEFEAT", headingStyle);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(x, y + 42, w, 34), "START NEW CAMPAIGN", buttonStyle)) StartNewCampaign();
            }
            GUI.EndScrollView();
        }

        void DrawBattle()
        {
            GUI.color = new Color(.03f, .04f, .04f, .8f);
            GUI.DrawTexture(new Rect(16, 80, Screen.width - 32, 84), pixel);
            GUI.color = Color.white;
            GUI.color = Accent;
            GUI.Label(new Rect(30, 88, 500, 28), activeBattle.Name.ToUpperInvariant(), titleStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(32, 121, 780, 25), "BLUE  " + CountTeam(1) + " formations     VS     RED  " + CountTeam(2) + " formations", bodyStyle);
            Rect roster = new Rect(18, Screen.height - 104, Screen.width - 36, 86);
            GUI.color = new Color(.03f, .04f, .04f, .86f);
            GUI.DrawTexture(roster, pixel);
            GUI.color = Color.white;
            int index = 0;
            for (int i = 0; i < battleUnits.Count; i++)
            {
                BattleUnit unit = battleUnits[i];
                if (unit.Team != 1) continue;
                float bw = (roster.width - 24) / Mathf.Max(1, Math.Min(12, CountTeam(1))) - 5;
                if (unit.Health <= 0) continue;
                Rect button = new Rect(roster.x + 12 + index * (bw + 5), roster.y + 10, bw, 58);
                string label = (index < 9 ? (index + 1) + "  " : "") + UnitCatalog.Get(unit.Source.Kind).Name + "\n" + Mathf.CeilToInt(Mathf.Clamp01(unit.Health / unit.Stats.MaxHealth) * 100) + "% health";
                GUIStyle style = i == selectedUnit ? selectedButtonStyle : smallStyle;
                if (GUI.Button(button, label, style)) SelectPlayerUnit(i);
                index++;
            }
            GUI.color = Accent;
            GUI.Label(new Rect(26, 168, Screen.width - 52, 34), "LMB / 1–9: select  ·  RMB: move  ·  F: hold  ·  Space: pause  ·  WASD: pan  ·  Q/E: rotate  ·  Wheel: zoom\nAuto-fire in range. Cover within 5m reduces damage. Time: " + Mathf.FloorToInt(battleElapsed) + "s", smallStyle);
            GUI.color = Color.white;
            if (GUI.Button(new Rect(Screen.width - 184, 92, 150, 42), "WITHDRAW", buttonStyle)) FinishBattle(false);
            if (GUI.Button(new Rect(Screen.width - 374, 92, 88, 42), battlePaused ? "RESUME" : "PAUSE", buttonStyle)) battlePaused = !battlePaused;
            if (GUI.Button(new Rect(Screen.width - 280, 92, 88, 42), battleSpeed + "x SPEED", buttonStyle)) battleSpeed = battleSpeed == 1f ? 2f : 1f;
        }

        void DrawResults()
        {
            Rect panel = new Rect(Screen.width * .23f, Screen.height * .22f, Screen.width * .54f, Screen.height * .56f);
            DrawPanel(panel, "AFTER-ACTION REPORT");
            GUI.color = lastResult != null && lastResult.PlayerWon ? new Color(.57f, .9f, .68f) : new Color(1f, .56f, .44f);
            GUI.Label(new Rect(panel.x + 28, panel.y + 74, panel.width - 56, 48), resultTitle, titleStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 30, panel.y + 136, panel.width - 60, 100), resultBody, bodyStyle);
            GUI.Label(new Rect(panel.x + 30, panel.y + 236, panel.width - 60, 54), "Campaign state updated. Reposition, recruit, and manage supplies before the next operation.", bodyStyle);
            if (GUI.Button(new Rect(panel.x + 30, panel.yMax - 60, panel.width - 60, 40), "RETURN TO THEATER MAP", buttonStyle)) screen = GameScreen.Campaign;
        }

        void DrawPanel(Rect rect, string caption)
        {
            GUI.color = Panel;
            GUI.DrawTexture(rect, pixel);
            GUI.color = new Color(.2f, .28f, .27f);
            GUI.DrawTexture(new Rect(rect.x, rect.y + 37, rect.width, 1), pixel);
            GUI.color = Muted;
            GUI.Label(new Rect(rect.x + 14, rect.y + 9, rect.width - 28, 24), caption, smallStyle);
            GUI.color = Color.white;
        }

        void RunCampaignAction(bool succeeded, string message)
        {
            notice = message;
            if (succeeded) selectedProvince = CampaignRules.GetProvince(campaign, campaign.ArmyProvinceId);
        }

        void SaveCampaign()
        {
            if (campaign.PendingBattle != null) { notice = "Finish the current operation before saving."; return; }
            try
            {
                if (!CampaignRules.Validate(campaign, out string reason)) { notice = "Save rejected: " + reason; return; }
                string path = Path.Combine(Application.persistentDataPath, SaveName);
                string temporary = path + ".tmp";
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(temporary, JsonUtility.ToJson(campaign, true));
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
                notice = "Campaign saved to the local player data folder.";
            }
            catch (Exception exception) { notice = "Save failed: " + exception.Message; }
        }

        void LoadCampaign()
        {
            string path = Path.Combine(Application.persistentDataPath, SaveName);
            try
            {
                if (!File.Exists(path)) { notice = "No local campaign save was found."; return; }
                if (new FileInfo(path).Length > 1048576) { notice = "Save rejected: file exceeds 1 MiB."; return; }
                CampaignState loaded = JsonUtility.FromJson<CampaignState>(File.ReadAllText(path));
                if (!CampaignRules.Validate(loaded, out string message)) { notice = "Save rejected: " + message; return; }
                campaign = loaded;
                selectedProvince = CampaignRules.GetProvince(campaign, campaign.ArmyProvinceId);
                notice = "Campaign loaded and validated.";
                if (campaign.PendingBattle != null) BeginBattle(campaign.PendingBattle.Copy());
            }
            catch (Exception exception) { notice = "Load failed: " + exception.Message; }
        }

        void StartNewCampaign()
        {
            campaign = CampaignRules.NewCampaign(DateTime.UtcNow.Millisecond + DateTime.UtcNow.Second * 1000);
            selectedProvince = CampaignRules.GetProvince(campaign, campaign.ArmyProvinceId);
            notice = "New operation started. Secure the theater.";
        }
    }
}
