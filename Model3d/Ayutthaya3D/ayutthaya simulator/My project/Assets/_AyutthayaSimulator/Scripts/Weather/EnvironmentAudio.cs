using UnityEngine;

namespace AyutthayaSimulator
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class EnvironmentAudio : MonoBehaviour
    {
        public AudioSource Source;
        public DayNightCycle Cycle;

        void Awake()
        {
            if (Source == null) Source = GetComponent<AudioSource>();
            Source.loop = true;
            Source.spatialBlend = 0f;
            Source.volume = 0.04f;
            if (Source.clip == null) Source.clip = ProceduralTone.Engine();
            Source.pitch = 0.35f;
        }

        void Update()
        {
            if (Cycle == null || Source == null) return;
            Source.volume = Mathf.Lerp(0.02f, 0.05f, Mathf.Clamp01(Cycle.SunElevation));
            if (!Source.isPlaying) Source.Play();
        }
    }
}
