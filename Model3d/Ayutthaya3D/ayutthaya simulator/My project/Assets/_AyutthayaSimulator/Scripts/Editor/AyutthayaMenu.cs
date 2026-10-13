using System.IO;
using AyutthayaSimulator.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AyutthayaSimulator.EditorTools
{
    public static class AyutthayaMenu
    {
        const string ScenePath = "Assets/_AyutthayaSimulator/Scenes/AyutthayaDemo.unity";

        [MenuItem("Tools/Ayutthaya Simulator/Create Player")]
        public static void CreatePlayer()
        {
            var player = new GameObject("Player");
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerInputHandler>();
            player.AddComponent<FirstPersonCamera>();
            player.AddComponent<PlayerInteraction>();
            player.AddComponent<PlayerController>();
            Selection.activeGameObject = player;
        }

        [MenuItem("Tools/Ayutthaya Simulator/Import Building")]
        public static void ImportBuilding() => BindSelected(ModelKind.Building);

        [MenuItem("Tools/Ayutthaya Simulator/Import Vehicle")]
        public static void ImportVehicle() => BindSelected(ModelKind.Vehicle);

        [MenuItem("Tools/Ayutthaya Simulator/Import NPC")]
        public static void ImportNpc() => BindSelected(ModelKind.Npc);

        [MenuItem("Tools/Ayutthaya Simulator/Generate Prefab")]
        public static void GeneratePrefab() => ModelPrefabBuilder.CreateFromSelection();

        [MenuItem("Tools/Ayutthaya Simulator/Auto Bind Components")]
        public static void AutoBind() => ComponentAutoBinder.BindSelection(false);

        [MenuItem("Tools/Ayutthaya Simulator/Generate Colliders")]
        public static void GenerateColliders() => ColliderGenerator.AddToSelection();

        [MenuItem("Tools/Ayutthaya Simulator/Setup LOD")]
        public static void SetupLod() => LODSetupTool.SetupSelection();

        [MenuItem("Tools/Ayutthaya Simulator/Validate Asset")]
        public static void ValidateAsset() => ModelImportValidator.ValidateSelection(true);

        [MenuItem("Tools/Ayutthaya Simulator/Generate Demo Scene")]
        public static void GenerateDemoScene() => AyutthayaBatch.GenerateScene(false);

        [MenuItem("Tools/Ayutthaya Simulator/Build Windows Game")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) GenerateDemoScene();
            var output = "Builds/Windows/AyutthayaSimulator.exe";
            Directory.CreateDirectory("Builds/Windows");
            var result = BuildPipeline.BuildPlayer(new[] { ScenePath }, output, BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.LogError("Windows build failed: " + result.summary.result);
            else
                Debug.Log("Windows build written to " + output + ". Distribute the whole Builds/Windows folder.");
        }

        static void BindSelected(ModelKind kind)
        {
            ComponentAutoBinder.BindSelection(true, kind);
        }
    }

    public static class AyutthayaBatch
    {
        public const string ScenePath = "Assets/_AyutthayaSimulator/Scenes/AyutthayaDemo.unity";

        public static void GenerateAndValidate()
        {
            var ok = GenerateScene(true);
            EditorApplication.Exit(ok ? 0 : 2);
        }

        public static bool GenerateScene(bool quitOnFailure)
        {
            Directory.CreateDirectory("Assets/_AyutthayaSimulator/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("AyutthayaBootstrap");
            var component = bootstrap.AddComponent<DemoSceneBootstrap>();
            component.BuildOnPlay = false;
            var world = DemoWorldBuilder.Build();
            var report = world != null ? world.GetComponent<DemoBuildReport>() : null;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddBuildScene(ScenePath);
            WriteReport(report);
            var ok = report != null && report.GisLoaded && report.Buildings > 0 && report.Roads > 0;
            if (!ok)
                Debug.LogError("Demo scene generation did not load GIS buildings and roads.");
            else
                Debug.Log("Demo scene generated. Buildings=" + report.Buildings + " Roads=" + report.Roads + " Water=" + report.Water);
            if (!ok && quitOnFailure) return false;
            return ok;
        }

        static void AddBuildScene(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var exists = false;
            for (var i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath) exists = true;
            }
            if (!exists) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void WriteReport(DemoBuildReport report)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "reports"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "unity-demo-build.json");
            var json = report == null
                ? "{\"passed\":false,\"error\":\"missing report\"}"
                : JsonUtility.ToJson(report, true);
            File.WriteAllText(path, json);
            Debug.Log("Wrote " + path);
        }
    }
}
