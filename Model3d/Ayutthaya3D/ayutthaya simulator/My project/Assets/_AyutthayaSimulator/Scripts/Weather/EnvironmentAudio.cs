using UnityEngine;

namespace AyutthayaSimulator
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class EnvironmentAudio : MonoBehaviour
    {
        public AudioSource Source;
        public DayNightCycle Cycle;
        public WeatherManager Weather;
        AudioSource nightSource;
        AudioClip dayClip;
        AudioClip nightClip;

        void Awake()
        {
            if (Source == null) Source = GetComponent<AudioSource>();
            Source.loop = true;
            Source.spatialBlend = 0f;
            Source.playOnAwake = false;
            Source.volume = 0f;
            dayClip = CreateAmbience(false);
            Source.clip = dayClip;
            Source.pitch = 1f;
            nightSource = gameObject.AddComponent<AudioSource>();
            nightSource.loop = true;
            nightSource.playOnAwake = false;
            nightSource.spatialBlend = 0f;
            nightSource.volume = 0f;
            nightClip = CreateAmbience(true);
            nightSource.clip = nightClip;
        }

        void Update()
        {
            if (Cycle == null || Source == null) return;
            var dry = 1f - (Weather != null ? Weather.RainAmount * 0.8f : 0f);
            Source.volume = Cycle.Daylight * 0.08f * dry;
            nightSource.volume = Cycle.NightAmount * 0.06f * dry;
            if (!Source.isPlaying) Source.Play();
            if (!nightSource.isPlaying) nightSource.Play();
        }

        static AudioClip CreateAmbience(bool night)
        {
            const int rate = 22050;
            var data = new float[rate * 8];
            for (var i = 0; i < data.Length; i++)
            {
                var t = i / (float)rate;
                var envelope = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * (night ? 4f : 0.75f))), night ? 12f : 20f);
                var frequency = night ? 3800f : 1700f;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t + Mathf.Sin(t * Mathf.PI * 8f) * 8f) * envelope * 0.35f;
            }
            var clip = AudioClip.Create(night ? "Night insects" : "Morning birds", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateRainClip()
        {
            const int rate = 22050;
            var data = new float[rate * 4];
            var random = new System.Random(761);
            var filtered = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                var noise = (float)random.NextDouble() * 2f - 1f;
                filtered = Mathf.Lerp(filtered, noise, 0.16f);
                data[i] = filtered * 0.7f + noise * 0.12f;
            }
            var clip = AudioClip.Create("Rain ambience", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (dayClip != null) Destroy(dayClip);
            if (nightClip != null) Destroy(nightClip);
        }
    }
}
