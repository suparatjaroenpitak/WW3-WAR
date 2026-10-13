using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class RainController : MonoBehaviour
    {
        public ParticleSystem Rain;
        public AudioSource RainAudio;

        public void SetRaining(bool raining)
        {
            if (Rain != null)
            {
                if (raining && !Rain.isPlaying) Rain.Play();
                if (!raining && Rain.isPlaying) Rain.Stop();
            }
            if (RainAudio != null) RainAudio.mute = !raining;
        }
    }
}
