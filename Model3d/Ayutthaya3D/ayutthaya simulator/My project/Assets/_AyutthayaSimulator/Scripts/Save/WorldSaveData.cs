using System;

namespace AyutthayaSimulator
{
    [Serializable]
    public sealed class WorldSaveData
    {
        public float GameHour = 10f;
        public string Weather = "Clear";
        public string[] CompletedMissionIds = Array.Empty<string>();
    }
}
