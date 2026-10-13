using System;
using System.Collections.Generic;

namespace AyutthayaSimulator.Core
{
    public readonly struct TileId : IEquatable<TileId>
    {
        public readonly int X;
        public readonly int Z;

        public TileId(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(TileId other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is TileId other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Z;
        public override string ToString() => X + "_" + Z;
    }

    /// <summary>
    /// Decides which world tiles should be loaded. Does not load assets.
    /// </summary>
    public static class TileStreamingPolicy
    {
        public static TileId TileFromLocal(double x, double z, double tileSize)
        {
            if (tileSize <= 0) throw new ArgumentOutOfRangeException(nameof(tileSize));
            return new TileId(FloorDiv(x, tileSize), FloorDiv(z, tileSize));
        }

        public static List<TileId> TilesInRadius(TileId center, int loadRadius)
        {
            if (loadRadius < 0) throw new ArgumentOutOfRangeException(nameof(loadRadius));
            var tiles = new List<TileId>((loadRadius * 2 + 1) * (loadRadius * 2 + 1));
            for (var dz = -loadRadius; dz <= loadRadius; dz++)
            {
                for (var dx = -loadRadius; dx <= loadRadius; dx++)
                {
                    tiles.Add(new TileId(center.X + dx, center.Z + dz));
                }
            }
            return tiles;
        }

        public static bool ShouldUnload(TileId loaded, TileId player, int unloadRadius)
        {
            var dx = Math.Abs(loaded.X - player.X);
            var dz = Math.Abs(loaded.Z - player.Z);
            return dx > unloadRadius || dz > unloadRadius;
        }

        public static bool ShouldShiftOrigin(double x, double z, double threshold)
        {
            return Math.Abs(x) > threshold || Math.Abs(z) > threshold;
        }

        static int FloorDiv(double value, double size)
        {
            return (int)Math.Floor(value / size);
        }
    }
}
