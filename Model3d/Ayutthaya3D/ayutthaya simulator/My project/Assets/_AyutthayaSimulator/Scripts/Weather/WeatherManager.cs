using UnityEngine;

namespace AyutthayaSimulator
{
    public enum WeatherKind
    {
        Clear,
        Rain
    }

    public sealed class WeatherManager : MonoBehaviour
    {
        public WeatherKind Current = WeatherKind.Clear;
        public RainController Rain;

        public void Toggle()
        {
            Set(Current == WeatherKind.Clear ? WeatherKind.Rain : WeatherKind.Clear);
        }

        public void Set(WeatherKind kind)
        {
            Current = kind;
            if (Rain != null) Rain.SetRaining(kind == WeatherKind.Rain);
        }
    }
}
