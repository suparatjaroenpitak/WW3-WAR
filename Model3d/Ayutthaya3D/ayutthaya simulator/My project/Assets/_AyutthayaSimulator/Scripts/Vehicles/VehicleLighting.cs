using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class VehicleLighting : MonoBehaviour
    {
        public DayNightCycle Cycle;
        public WeatherManager Weather;
        public Light[] Headlights;
        public Light[] TailLights;
        VehicleController vehicle;

        public void Configure(DayNightCycle cycle, WeatherManager weather)
        {
            Cycle = cycle;
            Weather = weather;
            vehicle = GetComponent<VehicleController>();
            if (Headlights == null || Headlights.Length == 0)
            {
                Headlights = new[] { CreateLight("Headlight L", new Vector3(-0.62f, 0.82f, 2.15f), false), CreateLight("Headlight R", new Vector3(0.62f, 0.82f, 2.15f), false) };
                TailLights = new[] { CreateLight("Tail light L", new Vector3(-0.65f, 0.7f, -2.15f), true), CreateLight("Tail light R", new Vector3(0.65f, 0.7f, -2.15f), true) };
            }
        }

        Light CreateLight(string label, Vector3 position, bool rear)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(rear ? 0f : 7f, rear ? 180f : 0f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = rear ? 110f : 65f;
            light.innerSpotAngle = rear ? 65f : 32f;
            light.range = rear ? 5f : 55f;
            light.color = rear ? new Color(1f, 0.025f, 0.015f) : new Color(0.87f, 0.93f, 1f);
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        void Update()
        {
            if (vehicle == null || Cycle == null) return;
            var amount = vehicle.Occupied ? Mathf.Max(Cycle.NightAmount, Weather != null ? Weather.RainAmount : 0f) : 0f;
            Apply(Headlights, amount, 5f);
            Apply(TailLights, amount, 2f);
        }

        static void Apply(Light[] lights, float amount, float intensity)
        {
            if (lights == null) return;
            foreach (var light in lights)
            {
                if (light == null) continue;
                light.enabled = amount > 0.01f;
                light.intensity = amount * intensity;
            }
        }
    }
}
