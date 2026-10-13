using UnityEngine;
using UnityEngine.Rendering;

namespace AyutthayaSimulator
{
    [DefaultExecutionOrder(-100)]
    public sealed class DayNightCycle : MonoBehaviour
    {
        public Light Sun;
        public Light Moon;
        public WeatherManager Weather;
        [Range(0f, 24f)] public float Hour = 10f;
        [Tooltip("0.5 game minutes per second gives a 48-minute real-time day.")]
        [Min(0f)] public float MinutesPerSecond = 0.5f;
        public bool Paused;
        public float SunElevation { get; private set; }
        public float Daylight { get; private set; }
        public float NightAmount => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.12f, SunElevation));
        public string ClockText => string.Format("{0:00}:{1:00}", Mathf.FloorToInt(Hour), Mathf.FloorToInt(Hour * 60f) % 60);
        Material sky;
        Material previousSky;

        void Start()
        {
            var shader = Resources.Load<Shader>("AyutthayaEnvironment/EnvironmentSky");
            if (shader != null)
            {
                previousSky = RenderSettings.skybox;
                sky = new Material(shader) { name = "Ayutthaya Dynamic Sky" };
                RenderSettings.skybox = sky;
            }
            ApplyLighting();
        }

        void Update() => SetHour(Hour + (Paused ? 0f : Mathf.Max(0f, MinutesPerSecond) * Time.deltaTime / 60f));
        void LateUpdate() => ApplyLighting();

        public void SetHour(float hour)
        {
            Hour = float.IsNaN(hour) || float.IsInfinity(hour) ? 10f : Mathf.Repeat(hour, 24f);
            SunElevation = Mathf.Sin((Hour - 6f) * Mathf.PI / 12f);
            Daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.16f, 0.35f, SunElevation));
        }

        public void ApplyLighting()
        {
            SetHour(Hour);
            var cloud = Weather != null ? Weather.CloudCover : 0f;
            var rain = Weather != null ? Weather.RainAmount : 0f;
            var warmth = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(SunElevation) / 0.42f)) * Daylight;
            if (Sun != null)
            {
                Sun.transform.rotation = Quaternion.Euler((Hour - 6f) * 15f, 165f, 0f);
                Sun.color = Color.Lerp(new Color(1f, 0.96f, 0.87f), new Color(1f, 0.43f, 0.16f), warmth);
                Sun.intensity = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.06f, 0.5f, SunElevation)) * 1.5f * (1f - cloud * 0.75f);
                Sun.shadowStrength = 1f - cloud * 0.65f;
            }
            if (Moon != null)
            {
                Moon.transform.rotation = Quaternion.Euler((Hour - 6f) * 15f + 180f, 165f, 0f);
                Moon.color = new Color(0.52f, 0.66f, 1f);
                Moon.intensity = NightAmount * 0.22f * (1f - cloud * 0.6f);
            }
            RenderSettings.sun = Sun != null && Sun.intensity > (Moon != null ? Moon.intensity : 0f) ? Sun : Moon;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var dimming = 1f - cloud * 0.48f;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.025f, 0.04f, 0.09f), new Color(0.48f, 0.61f, 0.78f) * dimming, Daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.025f, 0.03f, 0.05f), Color.Lerp(new Color(0.46f, 0.48f, 0.5f), new Color(0.65f, 0.35f, 0.2f), warmth) * dimming, Daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.015f, 0.02f, 0.03f), new Color(0.2f, 0.19f, 0.16f) * dimming, Daylight);
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.12f, 0.85f, Daylight) * dimming;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Lerp(0.0012f, 0.0055f, Mathf.Max(rain, cloud * 0.5f));
            RenderSettings.fogColor = Color.Lerp(new Color(0.025f, 0.035f, 0.065f), Color.Lerp(new Color(0.64f, 0.73f, 0.8f), new Color(0.36f, 0.4f, 0.45f), cloud), Daylight);
            if (sky == null) return;
            sky.SetFloat("_Daylight", Daylight);
            sky.SetFloat("_Warmth", warmth);
            sky.SetFloat("_CloudCover", cloud);
            sky.SetFloat("_Night", NightAmount);
            sky.SetColor("_Horizon", RenderSettings.fogColor);
            if (Sun != null) sky.SetVector("_SunDirection", -Sun.transform.forward);
            if (Moon != null) sky.SetVector("_MoonDirection", -Moon.transform.forward);
        }

        void OnDestroy()
        {
            if (sky == null) return;
            if (RenderSettings.skybox == sky) RenderSettings.skybox = previousSky;
            Destroy(sky);
        }
    }
}
