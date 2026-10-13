using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class WorldTile : MonoBehaviour
    {
        public int TileX;
        public int TileZ;
        public bool ContentLoaded;
        public string UnavailableReason = "";

        public TileId Id => new TileId(TileX, TileZ);

        public void MarkUnavailable(string reason)
        {
            ContentLoaded = false;
            UnavailableReason = reason;
        }
    }
}
