using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace WW3War.Editor
{
    public static class BuildCommands
    {
        public const string MainScene = "Assets/WW3War/Scenes/Main.unity";

        [MenuItem("WW3 War/Open Main Scene")]
        public static void OpenMainScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MainScene);
        }

        [MenuItem("WW3 War/Build Windows")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = "Builds/Windows/ww3 war.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("ww3 war build failed: " + report.summary.result);
            UnityEngine.Debug.Log("WW3 WAR: build complete at Builds/Windows/ww3 war.exe");
        }
    }
}
