using UnityEngine;

namespace AyutthayaSimulator
{
    public enum PlayerMode
    {
        OnFoot,
        Driving
    }

    public sealed class PlayerState
    {
        public PlayerMode Mode = PlayerMode.OnFoot;
        public bool Grounded = true;
        public bool Crouching;
        public bool Running;
        public Vector3 Velocity;
        public string Language = "th";
        public bool Paused;
    }
}
