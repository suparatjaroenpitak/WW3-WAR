using System.Collections.Generic;
using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class DemoBuildReport : MonoBehaviour
    {
        public bool GisLoaded;
        public string GisError = "";
        public string TerrainStatus = "PLACEHOLDER_FLAT_NO_DEM";
        public int Buildings;
        public int Roads;
        public int Water;
        public int Doors;
        public int EstimatedHeights;
        public int MeasuredHeights;
        public int EstimatedRoadWidths;
        public string OriginName = "";
        public string Attribution = "";
    }

    public static class DemoWorldBuilder
    {
        const float TileSize = 500f;
        const float Half = 250f;

        public static GameObject Build()
        {
            var existing = GameObject.Find("WorldRoot");
            if (existing != null) return existing;

            var root = new GameObject("WorldRoot");
            var report = root.AddComponent<DemoBuildReport>();
            var metadata = root.AddComponent<WorldMetadata>();
            metadata.TileSize = TileSize;

            var groundMaterial = MaterialUtil.Lit(new Color(0.45f, 0.48f, 0.34f), 0.05f, false);
            var roadMaterial = MaterialUtil.Lit(new Color(0.16f, 0.16f, 0.17f), 0.2f, false);
            var waterMaterial = MaterialUtil.Lit(new Color(0.15f, 0.35f, 0.55f, 0.8f), 0.6f, true);
            var doorMaterial = MaterialUtil.Lit(new Color(0.35f, 0.22f, 0.12f), 0.1f, false);

            CreateGround(root.transform, groundMaterial);
            UnityGisTile tile = null;
            string error;
            report.GisLoaded = GisTileLoader.TryLoad(out tile, out error);
            report.GisError = error;
            if (!report.GisLoaded)
            {
                Debug.LogWarning("Ayutthaya demo has no GIS tile. Flat placeholder only. " + error);
            }
            else
            {
                metadata.TileId = tile.tileId;
                metadata.TerrainStatus = string.IsNullOrEmpty(tile.terrainStatus) ? "PLACEHOLDER_FLAT_NO_DEM" : tile.terrainStatus;
                metadata.Attribution = tile.attribution;
                metadata.LicenseName = tile.licenseName;
                metadata.OriginName = tile.originName;
                metadata.OriginLatitude = tile.originLatitude;
                metadata.OriginLongitude = tile.originLongitude;
                report.TerrainStatus = metadata.TerrainStatus;
                report.OriginName = tile.originName;
                report.Attribution = tile.attribution;
                BuildFeatures(root.transform, tile, metadata, report, roadMaterial, waterMaterial, doorMaterial);
            }

            var spawn = FindSpawn(tile);
            var player = CreatePlayer(spawn);
            var vehicle = CreateVehicle(spawn + new Vector3(8f, 0.4f, 2f));
            var npc = CreateNpc(spawn + new Vector3(-2.5f, 0f, 2.5f));
            var cycle = CreateSun(root.transform);
            var weather = CreateWeather(player.transform);
            var missions = CreateMissions(root.transform, spawn);
            var saves = root.AddComponent<SaveManager>();
            var ui = GameUiController.Create(root.transform);
            ui.Player = player;
            ui.Missions = missions;
            ui.Vehicle = vehicle;
            ui.World = metadata;

            player.Missions = missions;
            player.Saves = saves;
            player.Weather = weather;
            player.Cycle = cycle;
            player.Ui = ui;

            var converter = root.AddComponent<GISCoordinateConverter>();
            if (tile != null)
            {
                converter.OriginLatitude = tile.originLatitude;
                converter.OriginLongitude = tile.originLongitude;
            }
            var streamer = root.AddComponent<WorldStreamer>();
            streamer.LoadRadius = 0;
            streamer.Player = player.transform;
            var tileMarker = root.AddComponent<WorldTile>();
            tileMarker.ContentLoaded = report.GisLoaded;
            tileMarker.TileX = 0;
            tileMarker.TileZ = 0;
            streamer.RegisterExisting(tileMarker);
            var origin = root.AddComponent<WorldOriginManager>();
            origin.Register(root.transform);
            var world = root.AddComponent<WorldManager>();
            world.Metadata = metadata;
            world.Streamer = streamer;
            world.Origin = origin;
            world.Coordinates = converter;
            world.Player = player.transform;
            root.AddComponent<InteractionRegistry>();
            var detector = root.AddComponent<InteractionDetector>();
            detector.Source = player.Interaction;
            return root;
        }

        static void CreateGround(Transform parent, Material material)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Terrain_PLACEHOLDER_NO_DEM";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(Half, -0.5f, Half);
            ground.transform.localScale = new Vector3(TileSize, 1f, TileSize);
            var renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            ground.isStatic = true;
        }

        static void BuildFeatures(Transform parent, UnityGisTile tile, WorldMetadata metadata, DemoBuildReport report, Material roadMaterial, Material waterMaterial, Material doorMaterial)
        {
            var buildingsRoot = new GameObject("Buildings").transform;
            buildingsRoot.SetParent(parent, false);
            var roadsRoot = new GameObject("Roads").transform;
            roadsRoot.SetParent(parent, false);
            var waterRoot = new GameObject("Water").transform;
            waterRoot.SetParent(parent, false);

            if (tile.buildings != null)
            {
                for (var i = 0; i < tile.buildings.Length; i++)
                {
                    if (CreateBuilding(buildingsRoot, tile.buildings[i], doorMaterial, report.Doors < 40))
                    {
                        report.Buildings++;
                        if (tile.buildings[i].heightSource == "osm_height_tag") report.MeasuredHeights++;
                        else report.EstimatedHeights++;
                        if (report.Doors < CountDoors(buildingsRoot)) report.Doors = CountDoors(buildingsRoot);
                    }
                }
            }
            if (tile.roads != null)
            {
                for (var i = 0; i < tile.roads.Length; i++)
                {
                    if (CreateRibbon(roadsRoot, "Road_" + Safe(tile.roads[i].id), tile.roads[i].points, tile.roads[i].widthMeters, tile.roads[i].bridge == 1 ? 4.2f : 0.04f, roadMaterial))
                    {
                        report.Roads++;
                        if (tile.roads[i].widthSource != "osm_width_tag") report.EstimatedRoadWidths++;
                    }
                }
            }
            if (tile.water != null)
            {
                for (var i = 0; i < tile.water.Length; i++)
                {
                    var water = tile.water[i];
                    var created = water.shape == "polygon"
                        ? CreatePolygon(waterRoot, "Water_" + Safe(water.id), water.points, 0.06f, waterMaterial)
                        : CreateRibbon(waterRoot, "Water_" + Safe(water.id), water.points, water.widthMeters, 0.05f, waterMaterial);
                    if (created) report.Water++;
                }
            }
            metadata.BuildingCount = report.Buildings;
            metadata.RoadCount = report.Roads;
            metadata.WaterCount = report.Water;
            metadata.EstimatedHeightCount = report.EstimatedHeights;
            metadata.MeasuredHeightCount = report.MeasuredHeights;
            CreateProceduralLights(parent, tile);
        }

        static bool CreateBuilding(Transform parent, UnityGisBuilding data, Material doorMaterial, bool allowDoor)
        {
            if (data == null || data.footprint == null || data.footprint.Length < 6) return false;
            var shifted = Shift(data.footprint);
            var height = Mathf.Clamp(data.heightMeters <= 0f ? 6f : data.heightMeters, 2.5f, 40f);
            var mesh = MeshFactory.ExtrudeFootprint(shifted, height);
            if (mesh == null) return false;
            var go = new GameObject("Building_" + Safe(data.id));
            go.transform.SetParent(parent, false);
            var metadata = go.AddComponent<BuildingMetadata>();
            metadata.OsmId = data.id;
            metadata.BuildingName = data.name;
            metadata.BuildingType = data.buildingType;
            metadata.HistoricTag = data.historic;
            metadata.HeightMeters = height;
            metadata.HeightSource = data.heightSource;
            metadata.Confidence = data.confidence;
            var lod0 = new GameObject("LOD0");
            lod0.transform.SetParent(go.transform, false);
            var filter = lod0.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = lod0.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialUtil.Lit(BuildingColor(data.id), 0.12f, false);
            var collider = lod0.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            lod0.isStatic = true;
            var lod1Object = new GameObject("LOD1");
            lod1Object.transform.SetParent(go.transform, false);
            var bounds = mesh.bounds;
            var box = MeshFactory.BoundsBox(bounds);
            var lod1Filter = lod1Object.AddComponent<MeshFilter>();
            lod1Filter.sharedMesh = box;
            var lod1Renderer = lod1Object.AddComponent<MeshRenderer>();
            lod1Renderer.sharedMaterial = renderer.sharedMaterial;
            lod1Object.isStatic = true;
            go.AddComponent<BuildingLODController>().Configure(renderer, lod1Renderer);
            go.AddComponent<BuildingController>();
            go.AddComponent<BuildingInteriorLoader>();
            if (allowDoor && PolygonTriangulator.Area(shifted) > 18f)
                CreateProceduralDoor(go.transform, shifted, height, doorMaterial, metadata);
            return true;
        }

        static void CreateProceduralDoor(Transform building, float[] footprint, float height, Material material, BuildingMetadata metadata)
        {
            var edge = LongestEdge(footprint);
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door_Procedural";
            door.transform.SetParent(building, false);
            var mid = (edge.a + edge.b) * 0.5f;
            var direction = (edge.b - edge.a).normalized;
            door.transform.position = new Vector3(mid.x, 0f, mid.y);
            door.transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y));
            door.transform.localScale = new Vector3(0.9f, Mathf.Min(2.2f, height * 0.7f), 0.08f);
            door.GetComponent<Renderer>().sharedMaterial = material;
            var controller = door.AddComponent<DoorController>();
            controller.PivotVerified = false;
            controller.Id = building.name + "_door";
            metadata.DoorPlacementIsProcedural = true;
            Object.DestroyImmediate(door.GetComponent<BoxCollider>());
            var box = door.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = Vector3.one;
        }

        static bool CreateRibbon(Transform parent, string name, float[] points, float width, float y, Material material)
        {
            if (points == null || points.Length < 4) return false;
            var mesh = MeshFactory.Ribbon(Shift(points), Mathf.Max(0.8f, width), y);
            return CreateMeshObject(parent, name, mesh, material, false);
        }

        static bool CreatePolygon(Transform parent, string name, float[] points, float y, Material material)
        {
            if (points == null) return false;
            var mesh = MeshFactory.FlatPolygon(Shift(points), y);
            return CreateMeshObject(parent, name, mesh, material, false);
        }

        static bool CreateMeshObject(Transform parent, string name, Mesh mesh, Material material, bool collider)
        {
            if (mesh == null) return false;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            return true;
        }

        static PlayerController CreatePlayer(Vector3 spawn)
        {
            var player = new GameObject("Player");
            player.transform.position = spawn + Vector3.up * 0.1f;
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerInputHandler>();
            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 900f;
            cameraObject.AddComponent<AudioListener>();
            var look = player.AddComponent<FirstPersonCamera>();
            look.CameraTransform = cameraObject.transform;
            var interaction = player.AddComponent<PlayerInteraction>();
            interaction.ViewCamera = camera;
            return player.AddComponent<PlayerController>();
        }

        static VehicleController CreateVehicle(Vector3 position)
        {
            var root = new GameObject("DemoVehicle_Procedural");
            root.transform.position = position;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            body.transform.localScale = new Vector3(1.8f, 0.7f, 4.2f);
            body.GetComponent<Renderer>().sharedMaterial = MaterialUtil.Lit(new Color(0.65f, 0.12f, 0.1f), 0.45f, false);
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.7f, 0f);
            box.size = new Vector3(1.8f, 0.9f, 4.2f);
            var wheels = root.AddComponent<VehicleWheelController>();
            wheels.FrontLeft = CreateWheel(root.transform, new Vector3(-0.8f, 0.35f, 1.35f), out wheels.VisualFrontLeft);
            wheels.FrontRight = CreateWheel(root.transform, new Vector3(0.8f, 0.35f, 1.35f), out wheels.VisualFrontRight);
            wheels.RearLeft = CreateWheel(root.transform, new Vector3(-0.8f, 0.35f, -1.35f), out wheels.VisualRearLeft);
            wheels.RearRight = CreateWheel(root.transform, new Vector3(0.8f, 0.35f, -1.35f), out wheels.VisualRearRight);
            var vehicle = root.AddComponent<VehicleController>();
            vehicle.VehicleId = "demo_vehicle";
            vehicle.Id = "demo_vehicle";
            vehicle.InputSource = root.AddComponent<VehicleInput>();
            vehicle.Engine = root.AddComponent<VehicleEngine>();
            vehicle.Wheels = wheels;
            vehicle.AudioSourceDriver = root.AddComponent<VehicleAudio>();
            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(0.35f, 0.9f, 0.2f);
            var exit = new GameObject("ExitPoint").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = new Vector3(1.6f, 0f, 0.2f);
            var enter = root.AddComponent<VehicleEnterExit>();
            enter.Seat = seat;
            enter.ExitPoint = exit;
            enter.Vehicle = vehicle;
            vehicle.EnterExit = enter;
            var cameraObject = new GameObject("VehicleCamera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            var listener = cameraObject.AddComponent<AudioListener>();
            listener.enabled = false;
            var driveCamera = cameraObject.AddComponent<VehicleCamera>();
            driveCamera.DriveCameraComponent = camera;
            driveCamera.LookTarget = root.transform;
            vehicle.DriveCamera = driveCamera;
            return vehicle;
        }

        static WheelCollider CreateWheel(Transform parent, Vector3 local, out Transform visual)
        {
            var go = new GameObject("WheelCollider");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var collider = go.AddComponent<WheelCollider>();
            collider.radius = 0.34f;
            collider.suspensionDistance = 0.18f;
            var spring = collider.suspensionSpring;
            spring.spring = 28000f;
            spring.damper = 3500f;
            spring.targetPosition = 0.4f;
            collider.suspensionSpring = spring;
            var visualObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualObject.name = "WheelVisual";
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = local;
            visualObject.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            visualObject.transform.localScale = new Vector3(0.68f, 0.12f, 0.68f);
            Object.DestroyImmediate(visualObject.GetComponent<Collider>());
            visualObject.GetComponent<Renderer>().sharedMaterial = MaterialUtil.Lit(new Color(0.08f, 0.08f, 0.08f), 0.3f, false);
            visual = visualObject.transform;
            return collider;
        }

        static NPCController CreateNpc(Vector3 position)
        {
            var root = new GameObject("GuideNpc");
            root.transform.position = position;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.GetComponent<Renderer>().sharedMaterial = MaterialUtil.Lit(new Color(0.85f, 0.75f, 0.45f), 0.1f, false);
            var dialogue = root.AddComponent<NPCDialogue>();
            dialogue.Id = "guide_npc";
            var agent = root.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.height = 1.8f;
            agent.radius = 0.3f;
            agent.speed = 1.3f;
            var npc = root.AddComponent<NPCController>();
            npc.Dialogue = dialogue;
            npc.Schedule = root.AddComponent<NPCSchedule>();
            npc.AnimationDriver = root.AddComponent<NPCAnimation>();
            npc.AnimationDriver.Visual = visual.transform;
            npc.Visual = visual.transform;
            npc.PointA = position;
            npc.PointB = position + new Vector3(0f, 0f, 8f);
            root.AddComponent<NPCSpawner>();
            return npc;
        }

        static DayNightCycle CreateSun(Transform parent)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            var cycle = go.AddComponent<DayNightCycle>();
            cycle.Sun = light;
            cycle.Hour = 10.5f;
            var lights = new List<Light>();
            for (var i = 0; i < 4; i++)
            {
                var lamp = new GameObject("StreetLight_Procedural_" + i);
                lamp.transform.SetParent(parent, false);
                lamp.transform.position = new Vector3(40f + i * 35f, 0f, 40f);
                var bulb = lamp.AddComponent<Light>();
                bulb.type = LightType.Point;
                bulb.range = 18f;
                bulb.intensity = 1.4f;
                bulb.color = new Color(1f, 0.9f, 0.7f);
                bulb.enabled = false;
                lights.Add(bulb);
            }
            var controller = go.AddComponent<StreetLightController>();
            controller.Lights = lights.ToArray();
            controller.Cycle = cycle;
            controller.PlacementIsProcedural = true;
            go.AddComponent<EnvironmentAudio>();
            return cycle;
        }

        static WeatherManager CreateWeather(Transform follow)
        {
            var go = new GameObject("Weather");
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startSpeed = 8f;
            main.startSize = 0.06f;
            main.startLifetime = 1.2f;
            main.maxParticles = 600;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 1f, 18f);
            var emission = particles.emission;
            emission.rateOverTime = 180f;
            particles.Stop();
            var rain = go.AddComponent<RainController>();
            rain.Rain = particles;
            var weather = go.AddComponent<WeatherManager>();
            weather.Rain = rain;
            go.transform.SetParent(follow, false);
            go.transform.localPosition = new Vector3(0f, 8f, 0f);
            return weather;
        }

        static MissionManager CreateMissions(Transform parent, Vector3 spawn)
        {
            var manager = parent.gameObject.AddComponent<MissionManager>();
            var reach = new MissionRecord
            {
                Id = "walk_to_marker",
                Kind = MissionKind.ReachPoint,
                TargetX = spawn.x + 18f,
                TargetZ = spawn.z + 8f,
                Radius = 5f
            };
            manager.Add(reach);
            manager.Add(new MissionRecord { Id = "talk_guide", Kind = MissionKind.TalkToNpc, TargetId = "guide_npc" });
            manager.Add(new MissionRecord { Id = "enter_demo_vehicle", Kind = MissionKind.EnterVehicle, TargetId = "demo_vehicle" });
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "MissionMarker_walk_to_marker";
            marker.transform.SetParent(parent, false);
            marker.transform.position = new Vector3(reach.TargetX, 0.05f, reach.TargetZ);
            marker.transform.localScale = new Vector3(2f, 0.05f, 2f);
            marker.GetComponent<Renderer>().sharedMaterial = MaterialUtil.Lit(new Color(0.95f, 0.8f, 0.15f), 0.2f, false);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            var triggerObject = new GameObject("MissionTrigger_walk_to_marker");
            triggerObject.transform.SetParent(parent, false);
            triggerObject.transform.position = marker.transform.position;
            var sphere = triggerObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 5f;
            var trigger = triggerObject.AddComponent<MissionTrigger>();
            trigger.MissionId = "walk_to_marker";
            trigger.Manager = manager;
            var objective = triggerObject.AddComponent<MissionObjective>();
            objective.MissionId = "walk_to_marker";
            objective.TitleThai = "เดินไปยังจุดสีเหลือง";
            objective.TitleEnglish = "Walk to the yellow marker";
            triggerObject.AddComponent<MissionReward>().MissionId = "walk_to_marker";
            return manager;
        }

        static Vector3 FindSpawn(UnityGisTile tile)
        {
            var fallback = new Vector3(Half + 12f, 0f, Half + 12f);
            if (tile == null || tile.roads == null) return fallback;
            var best = fallback;
            var bestScore = float.MaxValue;
            for (var r = 0; r < tile.roads.Length; r++)
            {
                var points = tile.roads[r].points;
                if (points == null) continue;
                for (var i = 0; i + 1 < points.Length; i += 2)
                {
                    var game = new Vector3(points[i] + Half, 0f, points[i + 1] + Half);
                    if (game.x < 8f || game.x > TileSize - 8f || game.z < 8f || game.z > TileSize - 8f) continue;
                    if (InsideBuilding(tile, points[i], points[i + 1])) continue;
                    var score = (game.x - Half) * (game.x - Half) + (game.z - Half) * (game.z - Half);
                    if (score < 36f) continue;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = game;
                    }
                }
            }
            return best;
        }

        static bool InsideBuilding(UnityGisTile tile, float x, float z)
        {
            if (tile.buildings == null) return false;
            for (var i = 0; i < tile.buildings.Length; i++)
            {
                if (PointInPolygon(tile.buildings[i].footprint, x, z)) return true;
            }
            return false;
        }

        static bool PointInPolygon(float[] packed, float x, float z)
        {
            if (packed == null || packed.Length < 6) return false;
            var inside = false;
            var count = MeshFactory.UniqueCount(packed);
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var xi = packed[i * 2];
                var zi = packed[i * 2 + 1];
                var xj = packed[j * 2];
                var zj = packed[j * 2 + 1];
                if (((zi > z) != (zj > z)) && (x < (xj - xi) * (z - zi) / ((zj - zi) + 0.00001f) + xi))
                    inside = !inside;
            }
            return inside;
        }

        static float[] Shift(float[] packed)
        {
            var copy = new float[packed.Length];
            for (var i = 0; i < packed.Length; i += 2)
            {
                copy[i] = packed[i] + Half;
                copy[i + 1] = packed[i + 1] + Half;
            }
            return copy;
        }

        static (Vector2 a, Vector2 b) LongestEdge(float[] packed)
        {
            var count = MeshFactory.UniqueCount(packed);
            var best = 0f;
            var a = Vector2.zero;
            var b = Vector2.right;
            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                var p = new Vector2(packed[i * 2], packed[i * 2 + 1]);
                var q = new Vector2(packed[next * 2], packed[next * 2 + 1]);
                var length = Vector2.Distance(p, q);
                if (length > best)
                {
                    best = length;
                    a = p;
                    b = q;
                }
            }
            return (a, b);
        }

        static int CountDoors(Transform root)
        {
            return root.GetComponentsInChildren<DoorController>(true).Length;
        }

        static Color BuildingColor(string id)
        {
            var hash = string.IsNullOrEmpty(id) ? 1 : id.GetHashCode();
            var t = Mathf.Abs(hash % 100) / 100f;
            return Color.Lerp(new Color(0.72f, 0.66f, 0.55f), new Color(0.55f, 0.5f, 0.45f), t);
        }

        static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unnamed";
            return value.Replace('/', '_').Replace('#', '_');
        }

        static void CreateProceduralLights(Transform parent, UnityGisTile tile)
        {
            if (tile == null) return;
            var note = new GameObject("PlacementNotes");
            note.transform.SetParent(parent, false);
            note.name = "StreetLights_Are_Procedural_Not_Surveyed";
        }
    }

    public static class MaterialUtil
    {
        public static Material Lit(Color color, float smoothness, bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (!transparent) return material;
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            return material;
        }
    }
}
