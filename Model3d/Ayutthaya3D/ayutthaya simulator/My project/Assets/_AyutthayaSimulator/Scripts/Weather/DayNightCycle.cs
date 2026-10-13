using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class DayNightCycle : MonoBehaviour
    {
        public Light Sun;
        public float Hour = 10f;
        public float MinutesPerSecond = 0.35f;
        public bool Paused;

        public float SunElevation { get; private set; }

        void Update()
        {
            if (!Paused) Hour = Mathf.Repeat(Hour + MinutesPerSecond * Time.deltaTime / 60f, 24f);
            if (Sun == null) return;
            var t = Hour / 24f;
            var elevation = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);
            SunElevation = elevation;
            Sun.transform.rotation = Quaternion.Euler(elevation * 80f, Hour * 15f, 0f);
            var day = Mathf.Clamp01(elevation);
            Sun.intensity = Mathf.Lerp(0.05f, 1.15f, day);
            Sun.color = Color.Lerp(new Color(0.55f, 0.65f, 0.9f), new Color(1f, 0.96f, 0.88f), day);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.05f, 0.06f, 0.09f), new Color(0.45f, 0.48f, 0.5f), day);
        }
    }
}
