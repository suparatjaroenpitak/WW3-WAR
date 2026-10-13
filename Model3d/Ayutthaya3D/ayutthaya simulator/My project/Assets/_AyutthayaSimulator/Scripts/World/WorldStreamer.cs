using System.Collections.Generic;
using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class WorldStreamer : MonoBehaviour
    {
        public float TileSize = 500f;
        public int LoadRadius = 1;
        public int UnloadRadius = 2;
        public Transform Player;

        readonly Dictionary<TileId, WorldTile> loaded = new Dictionary<TileId, WorldTile>();

        public int LoadedCount => loaded.Count;
        public string LastDecision = "";

        public void RegisterExisting(WorldTile tile)
        {
            if (tile == null) return;
            loaded[tile.Id] = tile;
        }

        public void Tick()
        {
            if (Player == null) return;
            var playerTile = TileStreamingPolicy.TileFromLocal(Player.position.x, Player.position.z, TileSize);
            var wanted = TileStreamingPolicy.TilesInRadius(playerTile, LoadRadius);
            for (var i = 0; i < wanted.Count; i++)
            {
                if (loaded.ContainsKey(wanted[i])) continue;
                var placeholder = new GameObject("Tile_" + wanted[i] + "_UNAVAILABLE").AddComponent<WorldTile>();
                placeholder.transform.SetParent(transform, false);
                placeholder.TileX = wanted[i].X;
                placeholder.TileZ = wanted[i].Z;
                placeholder.MarkUnavailable("No generated tile. Adjacent province tiles are not invented.");
                loaded[wanted[i]] = placeholder;
                LastDecision = "requested_missing_" + wanted[i];
            }

            var remove = new List<TileId>();
            foreach (var pair in loaded)
            {
                if (pair.Value != null && pair.Value.ContentLoaded) continue;
                if (TileStreamingPolicy.ShouldUnload(pair.Key, playerTile, UnloadRadius)) remove.Add(pair.Key);
            }
            for (var i = 0; i < remove.Count; i++)
            {
                var tile = loaded[remove[i]];
                loaded.Remove(remove[i]);
                if (tile != null) Destroy(tile.gameObject);
            }
        }
    }
}
