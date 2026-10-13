using System.Collections.Generic;
using UnityEngine;

namespace AyutthayaSimulator
{
    /// <summary>Upgrades both the baked demo and newly generated worlds at runtime.</summary>
    public sealed class EnvironmentSetup : MonoBehaviour
    {
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Material> roadMaterials = new List<Material>();
        readonly List<Color> roadColors = new List<Color>();
        WeatherManager weather;

        public static void Install(GameObject world)
        {
            if (world == null || world.GetComponent<EnvironmentSetup>() != null) return;
            world.AddComponent<EnvironmentSetup>().Initialize();
        }

        void Initialize()
        {
            var cycle = GetComponentInChildren<DayNightCycle>();
            var player = FindFirstObjectByType<PlayerController>();
            weather = player != null ? player.Weather : FindFirstObjectByType<WeatherManager>();
            if (cycle == null) return;
            cycle.Weather = weather;
            if (cycle.Moon == null)
            {
                var moon = new GameObject("Moon");
                moon.transform.SetParent(transform, false);
                cycle.Moon = moon.AddComponent<Light>();
                cycle.Moon.type = LightType.Directional;
                cycle.Moon.shadows = LightShadows.Soft;
            }
            var ambience = cycle.GetComponent<EnvironmentAudio>();
            if (ambience == null) ambience = cycle.gameObject.AddComponent<EnvironmentAudio>();
            ambience.Cycle = cycle;
            ambience.Weather = weather;
            if (weather != null && weather.Rain != null)
                weather.Rain.Configure(player != null ? player.transform : null);

            var controller = cycle.GetComponent<StreetLightController>();
            if (controller == null) controller = cycle.gameObject.AddComponent<StreetLightController>();
            controller.Cycle = cycle;
            controller.Weather = weather;
            // Only replace the demo's explicitly procedural placeholder lights.
            if (controller.PlacementIsProcedural)
            {
                foreach (var old in controller.Lights)
                    if (old != null) { old.enabled = false; Destroy(old.gameObject); }
                BuildStreetLights(controller, player);
            }
            var roads = transform.Find("Roads");
            if (roads != null)
            {
                var copies = new Dictionary<Material, Material>();
                foreach (var renderer in roads.GetComponentsInChildren<Renderer>())
                {
                    var original = renderer.sharedMaterial;
                    if (original == null) continue;
                    if (!copies.TryGetValue(original, out var material))
                    {
                        material = new Material(original);
                        copies.Add(original, material);
                        ownedMaterials.Add(material);
                        roadMaterials.Add(material);
                        roadColors.Add(material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color);
                    }
                    renderer.sharedMaterial = material;
                }
            }
            foreach (var vehicle in FindObjectsByType<VehicleController>(FindObjectsSortMode.None))
            {
                var lights = vehicle.GetComponent<VehicleLighting>();
                if (lights == null) lights = vehicle.gameObject.AddComponent<VehicleLighting>();
                lights.Configure(cycle, weather);
            }
            cycle.ApplyLighting();
        }

        void BuildStreetLights(StreetLightController controller, PlayerController player)
        {
            var positions = new List<Vector3>();
            // Start with a usable lamp by the player, then sample actual road meshes.
            if (player != null) positions.Add(player.transform.position + new Vector3(4f, 0f, 4f));
            var roads = transform.Find("Roads");
            if (roads != null)
            {
                foreach (var filter in roads.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null) continue;
                    var vertices = filter.sharedMesh.vertices;
                    // Ribbon vertices come in left/right pairs: the first lies at the road edge.
                    for (var i = 0; i + 1 < vertices.Length && positions.Count < 32; i += 2)
                    {
                        var edge = filter.transform.TransformPoint(vertices[i]);
                        var opposite = filter.transform.TransformPoint(vertices[i + 1]);
                        var candidate = edge + (edge - opposite).normalized * 0.8f;
                        if (positions.Exists(p => (p - candidate).sqrMagnitude < 28f * 28f)) continue;
                        positions.Add(candidate);
                    }
                    if (positions.Count >= 32) break;
                }
            }
            var group = new GameObject("StreetLights_Procedural_NotSurveyed").transform;
            group.SetParent(transform, false);
            var metal = MaterialUtil.Lit(new Color(0.13f, 0.15f, 0.16f), 0.4f, false);
            var glow = MaterialUtil.Lit(new Color(1f, 0.85f, 0.58f), 0.3f, false);
            glow.EnableKeyword("_EMISSION");
            ownedMaterials.Add(metal);
            ownedMaterials.Add(glow);
            var lights = new List<Light>();
            var bulbs = new List<Renderer>();
            foreach (var position in positions)
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Street lamp";
                pole.transform.SetParent(group, false);
                pole.transform.position = position + Vector3.up * 2.6f;
                pole.transform.localScale = new Vector3(0.12f, 2.6f, 0.12f);
                pole.GetComponent<Renderer>().sharedMaterial = metal;
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Lamp diffuser";
                bulb.transform.SetParent(group, false);
                bulb.transform.position = position + Vector3.up * 5.2f;
                bulb.transform.localScale = new Vector3(0.45f, 0.24f, 0.45f);
                Destroy(bulb.GetComponent<Collider>());
                var renderer = bulb.GetComponent<Renderer>();
                renderer.sharedMaterial = glow;
                bulbs.Add(renderer);
                var light = bulb.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.76f, 0.43f);
                light.range = 19f;
                light.shadows = LightShadows.None;
                light.enabled = false;
                lights.Add(light);
            }
            controller.Lights = lights.ToArray();
            controller.Bulbs = bulbs.ToArray();
        }

        void Update()
        {
            if (weather == null) return;
            for (var i = 0; i < roadMaterials.Count; i++)
            {
                var material = roadMaterials[i];
                var color = roadColors[i] * Mathf.Lerp(1f, 0.57f, weather.Wetness);
                color.a = roadColors[i].a;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", Mathf.Lerp(0.2f, 0.88f, weather.Wetness));
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", Mathf.Lerp(0.2f, 0.88f, weather.Wetness));
            }
        }

        void OnDestroy()
        {
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
        }
    }
}
