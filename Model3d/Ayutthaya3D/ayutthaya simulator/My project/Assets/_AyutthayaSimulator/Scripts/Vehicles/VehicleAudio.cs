using UnityEngine;

namespace AyutthayaSimulator
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class VehicleAudio : MonoBehaviour
    {
        public VehicleEngine Engine;
        AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.spatialBlend = 1f;
            source.playOnAwake = false;
            if (source.clip == null) source.clip = ProceduralTone.Engine();
        }

        void Update()
        {
            if (Engine == null || source.clip == null) return;
            var speed = Engine.CurrentSpeedKph;
            if (speed < 0.5f)
            {
                if (source.isPlaying) source.Stop();
                return;
            }
            if (!source.isPlaying) source.Play();
            source.pitch = Mathf.Lerp(0.8f, 1.6f, Mathf.Clamp01(speed / 60f));
            source.volume = 0.15f;
        }
    }
}
