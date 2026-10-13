using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class NPCSchedule : MonoBehaviour
    {
        public float DayStartHour = 6f;
        public float NightStartHour = 19f;
        public bool OutsideDuringDay = true;

        public bool ShouldBeOutside(float hour)
        {
            var day = hour >= DayStartHour && hour < NightStartHour;
            return day ? OutsideDuringDay : !OutsideDuringDay;
        }
    }
}
