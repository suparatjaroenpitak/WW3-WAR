using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AyutthayaSimulator.Editor
{
    /// <summary>Batch Play Mode smoke checks against the actual baked demo.</summary>
    [InitializeOnLoad]
    public static class EnvironmentValidation
    {
        const string Key = "AyutthayaEnvironmentValidation";
        static int stage;
        static double next;
        static double deadline;
        static float previousHour;
        static float previousWetness;
        static DayNightCycle cycle;
        static WeatherManager weather;
        static PlayerController player;
        static double readyAt;
        static string Output => Path.GetFullPath("Logs/EnvironmentValidation");

        static EnvironmentValidation()
        {
            if (SessionState.GetBool(Key, false))
            {
                Arm();
            }
        }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/_AyutthayaSimulator/Scenes/AyutthayaDemo.unity");
            SessionState.SetBool(Key, true);
            Arm();
        }

        static void Arm()
        {
            readyAt = EditorApplication.timeSinceStartup + 15;
            EditorApplication.update -= WaitForStartup;
            EditorApplication.update += WaitForStartup;
        }

        static void WaitForStartup()
        {
            // Package initialization may change scripting defines after executeMethod.
            // Let that compilation settle before entering Play Mode.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                readyAt = EditorApplication.timeSinceStartup + 5;
                return;
            }
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= WaitForStartup;
            stage = 0;
            deadline = EditorApplication.timeSinceStartup + 120;
            next = EditorApplication.timeSinceStartup + 3;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Debug.Log("ENV PASS: " + message);
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish("Timed out waiting for Play Mode"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
            try
            {
                switch (stage++)
                {
                    case 0:
                        cycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
                        weather = UnityEngine.Object.FindFirstObjectByType<WeatherManager>();
                        player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                        Check(cycle != null && weather != null && player != null, "Baked scene has environment and player");
                        Check(cycle.Moon != null && cycle.Weather == weather, "Bootstrap connected moon and weather");
                        Check(RenderSettings.skybox != null && RenderSettings.skybox.shader.name == "Ayutthaya/EnvironmentSky", "Dynamic sky is active");
                        Check(!ShaderUtil.ShaderHasError(RenderSettings.skybox.shader), "Sky shader compiled");
                        Check(weather.Rain.Follow == player.transform, "Rain follows player");
                        Check(Vector3.Dot(weather.Rain.transform.forward, Vector3.down) > 0.99f, "Rain falls toward the ground");
                        Check(cycle.GetComponent<StreetLightController>().Lights.Length > 4, "Roadside lamps generated");
                        cycle.SetHour(24.5f);
                        Check(Mathf.Abs(cycle.Hour - 0.5f) < 0.001f, "Clock wraps midnight");
                        cycle.SetHour(-1f);
                        Check(Mathf.Abs(cycle.Hour - 23f) < 0.001f, "Negative clock wraps");
                        cycle.SetHour(float.NaN);
                        Check(cycle.Hour == 10f, "Invalid clock recovers");
                        weather.AutomaticWeather = false;
                        weather.Set(WeatherKind.Clear, true);
                        cycle.Paused = true;
                        cycle.SetHour(12f);
                        break;
                    case 1:
                        Check(cycle.Sun.intensity > 1f && cycle.Moon.intensity < 0.01f, "Noon uses sun lighting");
                        Check(!cycle.GetComponent<StreetLightController>().Lights[0].enabled, "Street lights off at noon");
                        Capture("noon.png");
                        break;
                    case 2:
                        cycle.SetHour(17.6f);
                        break;
                    case 3:
                        Capture("sunset.png");
                        break;
                    case 4:
                        cycle.SetHour(0f);
                        var vehicle = UnityEngine.Object.FindFirstObjectByType<VehicleController>();
                        Check(vehicle.GetComponent<VehicleLighting>() != null, "Vehicle has automatic lights");
                        vehicle.SetOccupied(true);
                        break;
                    case 5:
                        Check(cycle.Sun.intensity < 0.001f && cycle.Moon.intensity > 0.1f, "Midnight uses moon lighting");
                        Check(cycle.GetComponent<StreetLightController>().Lights[0].enabled, "Street lights on at midnight");
                        var lighting = UnityEngine.Object.FindFirstObjectByType<VehicleLighting>();
                        Check(lighting.Headlights[0].enabled, "Occupied vehicle headlights on at night");
                        lighting.GetComponent<VehicleController>().SetOccupied(false);
                        Capture("night.png");
                        break;
                    case 6:
                        weather.TransitionSeconds = 1f;
                        weather.Set(WeatherKind.Storm);
                        Check(weather.RainAmount == 0f, "Weather transition does not snap rain");
                        cycle.SetHour(12f);
                        break;
                    case 7:
                        Check(weather.RainAmount > 0f && weather.RainAmount < 1f, "Rain transitions gradually");
                        next = EditorApplication.timeSinceStartup + 2;
                        return;
                    case 8:
                        Check(weather.RainAmount > 0.99f && weather.Wetness > 0f, "Heavy rain wets the roads");
                        Check(weather.Rain.Rain.isPlaying, "Rain particles are playing");
                        Check(weather.Rain.Rain.particleCount > 0, "Rain particles are emitted");
                        Capture("rain.png");
                        previousWetness = weather.Wetness;
                        weather.Set(WeatherKind.Clear);
                        next = EditorApplication.timeSinceStartup + 2;
                        return;
                    case 9:
                        Check(weather.RainAmount < 0.01f && weather.Wetness > 0f, "Wet roads persist after rain stops");
                        previousWetness = weather.Wetness;
                        previousHour = cycle.Hour;
                        cycle.Paused = false;
                        player.SetPaused(true);
                        break;
                    case 10:
                        Check(cycle.Hour == previousHour && weather.Wetness == previousWetness, "Pause freezes time and weather simulation");
                        player.SetPaused(false);
                        cycle.Paused = true;
                        var saves = player.Saves;
                        var originalName = saves.FileName;
                        saves.FileName = "environment-validation-" + Guid.NewGuid().ToString("N") + ".json";
                        try
                        {
                            foreach (WeatherKind kind in Enum.GetValues(typeof(WeatherKind)))
                            {
                                weather.Set(kind, true);
                                cycle.SetHour(23.75f);
                                Check(saves.Save(player, cycle, weather, player.Missions), "Save " + kind);
                                weather.Set(WeatherKind.Clear, true);
                                cycle.SetHour(6f);
                                Check(saves.Load(player, cycle, weather, player.Missions), "Load " + kind);
                                Check(weather.Current == kind && cycle.Hour == 23.75f, "Time and weather round trip " + kind);
                            }
                        }
                        finally
                        {
                            if (File.Exists(saves.PathOnDisk)) File.Delete(saves.PathOnDisk);
                            saves.FileName = originalName;
                        }
                        Finish(null);
                        return;
                }
                next = EditorApplication.timeSinceStartup + 0.25;
            }
            catch (Exception ex) { Finish(ex.ToString()); }
        }

        static void Capture(string name)
        {
            // Batch mode has no Game view backbuffer. Render the actual player camera.
            var camera = player.Interaction.ViewCamera;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousAspect = camera.aspect;
            try
            {
                target.Create();
                camera.aspect = 1280f / 720f;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, name), pixels.EncodeToPNG());
            }
            finally
            {
                camera.aspect = previousAspect;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        static void Finish(string error)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            File.WriteAllText(Path.Combine(Output, "result.txt"), error ?? "All environment Play Mode checks passed.");
            if (error != null) Debug.LogError("ENV FAIL: " + error);
            else Debug.Log("ENVIRONMENT VALIDATION PASSED");
            EditorApplication.Exit(error == null ? 0 : 2);
        }
    }
}
