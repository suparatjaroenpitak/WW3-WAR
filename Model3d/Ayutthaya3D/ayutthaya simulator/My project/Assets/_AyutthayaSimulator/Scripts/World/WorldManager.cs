using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class WorldManager : MonoBehaviour
    {
        public WorldMetadata Metadata;
        public WorldStreamer Streamer;
        public WorldOriginManager Origin;
        public GISCoordinateConverter Coordinates;
        public Transform Player;

        void Update()
        {
            if (Streamer != null)
            {
                Streamer.Player = Player;
                Streamer.Tick();
            }
            if (Origin != null && Player != null)
                Origin.ShiftIfNeeded(Player.position, Player);
        }
    }
}
