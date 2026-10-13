using System;
using System.Collections.Generic;

namespace AyutthayaSimulator.Core
{
    /// <summary>
    /// Ear-clipping triangulation for simple XZ footprints. Holes are not supported.
    /// </summary>
    public static class PolygonTriangulator
    {
        public static int[] Triangulate(float[] packedXZ)
        {
            if (packedXZ == null || packedXZ.Length < 6) return Array.Empty<int>();
            var count = packedXZ.Length / 2;
            if (count >= 2 && Nearly(packedXZ[0], packedXZ[packedXZ.Length - 2]) && Nearly(packedXZ[1], packedXZ[packedXZ.Length - 1]))
                count -= 1;
            if (count < 3) return Array.Empty<int>();

            var indices = new List<int>(count);
            for (var i = 0; i < count; i++) indices.Add(i);
            if (SignedArea(packedXZ, indices) < 0f)
                indices.Reverse();

            var triangles = new List<int>((count - 2) * 3);
            var guard = 0;
            while (indices.Count > 3 && guard < count * count)
            {
                var clipped = false;
                for (var i = 0; i < indices.Count; i++)
                {
                    var prev = indices[(i + indices.Count - 1) % indices.Count];
                    var current = indices[i];
                    var next = indices[(i + 1) % indices.Count];
                    if (!IsConvex(packedXZ, prev, current, next)) continue;
                    if (ContainsPoint(packedXZ, indices, prev, current, next)) continue;
                    triangles.Add(prev);
                    triangles.Add(current);
                    triangles.Add(next);
                    indices.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
                guard++;
            }
            if (indices.Count == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[1]);
                triangles.Add(indices[2]);
            }
            return triangles.ToArray();
        }

        public static float Area(float[] packedXZ)
        {
            if (packedXZ == null || packedXZ.Length < 6) return 0f;
            var count = packedXZ.Length / 2;
            double sum = 0;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                sum += packedXZ[i * 2] * packedXZ[j * 2 + 1] - packedXZ[j * 2] * packedXZ[i * 2 + 1];
            }
            return (float)Math.Abs(sum * 0.5);
        }

        static float SignedArea(float[] packed, List<int> indices)
        {
            double sum = 0;
            for (var i = 0; i < indices.Count; i++)
            {
                var j = (i + 1) % indices.Count;
                var a = indices[i] * 2;
                var b = indices[j] * 2;
                sum += packed[a] * packed[b + 1] - packed[b] * packed[a + 1];
            }
            return (float)(sum * 0.5);
        }

        static bool IsConvex(float[] packed, int prev, int current, int next)
        {
            var ax = packed[current * 2] - packed[prev * 2];
            var az = packed[current * 2 + 1] - packed[prev * 2 + 1];
            var bx = packed[next * 2] - packed[current * 2];
            var bz = packed[next * 2 + 1] - packed[current * 2 + 1];
            return ax * bz - az * bx > 1e-6f;
        }

        static bool ContainsPoint(float[] packed, List<int> indices, int prev, int current, int next)
        {
            for (var i = 0; i < indices.Count; i++)
            {
                var index = indices[i];
                if (index == prev || index == current || index == next) continue;
                if (PointInTriangle(packed, index, prev, current, next)) return true;
            }
            return false;
        }

        static bool PointInTriangle(float[] packed, int p, int a, int b, int c)
        {
            var px = packed[p * 2];
            var pz = packed[p * 2 + 1];
            var cross1 = Cross(packed, a, b, px, pz);
            var cross2 = Cross(packed, b, c, px, pz);
            var cross3 = Cross(packed, c, a, px, pz);
            var hasNeg = cross1 < 0f || cross2 < 0f || cross3 < 0f;
            var hasPos = cross1 > 0f || cross2 > 0f || cross3 > 0f;
            return !(hasNeg && hasPos);
        }

        static float Cross(float[] packed, int a, int b, float px, float pz)
        {
            return (packed[b * 2] - packed[a * 2]) * (pz - packed[a * 2 + 1]) - (packed[b * 2 + 1] - packed[a * 2 + 1]) * (px - packed[a * 2]);
        }

        static bool Nearly(float a, float b) => Math.Abs(a - b) < 0.001f;
    }
}
