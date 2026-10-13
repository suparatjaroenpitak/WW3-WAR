using UnityEngine;

namespace AyutthayaSimulator
{
    // Preserve the original values for serialized scenes and saves.
    public enum WeatherKind { Clear = 0, Rain = 1, Overcast = 2, Storm = 3 }

    [DefaultExecutionOrder(-90)]
    public sealed class WeatherManager : MonoBehaviour
    {
        public WeatherKind Current = WeatherKind.Clear;
        public RainController Rain;
        public bool AutomaticWeather = true;
        [Min(1f)] public float TransitionSeconds = 18f;
        public Vector2 WeatherDurationSeconds = new Vector2(180f, 420f);
        public float CloudCover { get; private set; }
        public float RainAmount { get; private set; }
        public float Wetness { get; private set; }
        float remaining;

        void Start() => Set(Current, true);

        void Update()
        {
            if (AutomaticWeather && Time.deltaTime > 0f)
            {
                remaining -= Time.deltaTime;
                if (remaining <= 0f) Set(NextAutomatic());
            }
            var step = Time.deltaTime / Mathf.Max(1f, TransitionSeconds);
            CloudCover = Mathf.MoveTowards(CloudCover, TargetCloud(), step);
            RainAmount = Mathf.MoveTowards(RainAmount, TargetRain(), step);
            // Road surfaces take longer to dry than the rain takes to stop.
            Wetness = Mathf.MoveTowards(Wetness, RainAmount, Time.deltaTime / (RainAmount > Wetness ? 45f : 150f));
            if (Rain != null) Rain.SetIntensity(RainAmount);
        }

        WeatherKind NextAutomatic()
        {
            switch (Current)
            {
                case WeatherKind.Clear: return WeatherKind.Overcast;
                case WeatherKind.Overcast: return Random.value < 0.65f ? WeatherKind.Rain : WeatherKind.Clear;
                case WeatherKind.Rain: return Random.value < 0.3f ? WeatherKind.Storm : WeatherKind.Overcast;
                default: return WeatherKind.Rain;
            }
        }

        public void Toggle()
        {
            Set(Current == WeatherKind.Clear ? WeatherKind.Overcast : Current == WeatherKind.Overcast ? WeatherKind.Rain : Current == WeatherKind.Rain ? WeatherKind.Storm : WeatherKind.Clear);
        }

        public void Set(WeatherKind kind) => Set(kind, false);

        public void Set(WeatherKind kind, bool immediate)
        {
            Current = System.Enum.IsDefined(typeof(WeatherKind), kind) ? kind : WeatherKind.Clear;
            remaining = Random.Range(Mathf.Max(10f, WeatherDurationSeconds.x), Mathf.Max(10f, Mathf.Max(WeatherDurationSeconds.x, WeatherDurationSeconds.y)));
            if (immediate)
            {
                CloudCover = TargetCloud();
                RainAmount = TargetRain();
                Wetness = RainAmount;
                if (Rain != null) Rain.SetIntensity(RainAmount);
            }
        }

        float TargetCloud() => Current == WeatherKind.Clear ? 0.12f : Current == WeatherKind.Overcast ? 0.72f : Current == WeatherKind.Rain ? 0.86f : 1f;
        float TargetRain() => Current == WeatherKind.Rain ? 0.55f : Current == WeatherKind.Storm ? 1f : 0f;
    }
}
