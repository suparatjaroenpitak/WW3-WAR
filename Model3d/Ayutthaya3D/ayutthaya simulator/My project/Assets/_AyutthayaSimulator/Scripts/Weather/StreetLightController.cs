using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class StreetLightController : MonoBehaviour
    {
        public Light[] Lights = System.Array.Empty<Light>();
        public DayNightCycle Cycle;
        public bool PlacementIsProcedural = true;

        void Update()
        {
            if (Cycle == null) return;
            var on = Cycle.SunElevation < 0.05f;
            for (var i = 0; i < Lights.Length; i++)
            {
                if (Lights[i] != null) Lights[i].enabled = on;
            }
        }
    }
}
