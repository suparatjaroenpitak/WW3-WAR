using UnityEngine;

namespace AyutthayaSimulator
{
    public static class ProceduralTone
    {
        public static AudioClip Step()
        {
            return Make("step", 900, 9000, (i, length) => Mathf.Sin(i * 0.55f) * Mathf.Exp(-i / 70f) * 0.25f);
        }

        public static AudioClip Engine()
        {
            return Make("engine", 4000, 8000, (i, length) =>
            {
                var t = i / 8000f;
                return (Mathf.Sin(t * 180f) * 0.15f + Mathf.Sin(t * 90f) * 0.08f) * 0.4f;
            });
        }

        static AudioClip Make(string name, int samples, int rate, System.Func<int, int, float> sample)
        {
            var clip = AudioClip.Create(name, samples, 1, rate, false);
            var data = new float[samples];
            for (var i = 0; i < samples; i++) data[i] = sample(i, samples);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
