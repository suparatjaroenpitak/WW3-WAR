using System;

namespace AyutthayaSimulator
{
    [Serializable]
    public sealed class PlayerSaveData
    {
        public float X;
        public float Y;
        public float Z;
        public float Yaw;
        public string Language = "th";
        public float LookSensitivity = 0.08f;
    }
}
