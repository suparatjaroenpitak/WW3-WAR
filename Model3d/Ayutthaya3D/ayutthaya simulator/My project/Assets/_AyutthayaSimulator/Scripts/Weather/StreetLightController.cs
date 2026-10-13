using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class StreetLightController : MonoBehaviour
    {
        public Light[] Lights = System.Array.Empty<Light>();
        public DayNightCycle Cycle;
        public bool PlacementIsProcedural = true;
        public Renderer[] Bulbs = System.Array.Empty<Renderer>();
        public WeatherManager Weather;
        public float Intensity = 2.6f;
        MaterialPropertyBlock properties;

        void Update()
        {
            if (Cycle == null) return;
            var amount = Mathf.Max(Cycle.NightAmount, Weather != null ? Weather.RainAmount * 0.65f : 0f);
            for (var i = 0; i < Lights.Length; i++)
            {
                if (Lights[i] == null) continue;
                Lights[i].enabled = amount > 0.01f;
                Lights[i].intensity = Intensity * amount;
            }
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.32f) * amount * 3f);
            foreach (var bulb in Bulbs)
                if (bulb != null) bulb.SetPropertyBlock(properties);
        }
    }
}
