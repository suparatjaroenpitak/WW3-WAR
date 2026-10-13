using System.Collections.Generic;
using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    public static class MeshFactory
    {
        public static Mesh ExtrudeFootprint(float[] packedXZ, float height)
        {
            var triangles = PolygonTriangulator.Triangulate(packedXZ);
            if (triangles.Length < 3) return null;
            var count = UniqueCount(packedXZ);
            var vertices = new List<Vector3>(count * 2 + count * 4);
            var uv = new List<Vector2>();
            var indices = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var x = packedXZ[i * 2];
                var z = packedXZ[i * 2 + 1];
                vertices.Add(new Vector3(x, height, z));
                uv.Add(new Vector2(x * 0.05f, z * 0.05f));
            }
            for (var i = 0; i < triangles.Length; i += 3)
            {
                indices.Add(triangles[i]);
                indices.Add(triangles[i + 2]);
                indices.Add(triangles[i + 1]);
            }
            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                var a = vertices[i];
                var b = vertices[next];
                var start = vertices.Count;
                vertices.Add(new Vector3(a.x, 0f, a.z));
                vertices.Add(new Vector3(b.x, 0f, b.z));
                vertices.Add(new Vector3(b.x, height, b.z));
                vertices.Add(new Vector3(a.x, height, a.z));
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(1f, 0f));
                uv.Add(new Vector2(1f, 1f));
                uv.Add(new Vector2(0f, 1f));
                indices.Add(start);
                indices.Add(start + 1);
                indices.Add(start + 2);
                indices.Add(start);
                indices.Add(start + 2);
                indices.Add(start + 3);
            }
            return Build(vertices, indices, uv);
        }

        public static Mesh FlatPolygon(float[] packedXZ, float y)
        {
            var triangles = PolygonTriangulator.Triangulate(packedXZ);
            if (triangles.Length < 3) return null;
            var count = UniqueCount(packedXZ);
            var vertices = new List<Vector3>(count);
            var uv = new List<Vector2>(count);
            for (var i = 0; i < count; i++)
            {
                vertices.Add(new Vector3(packedXZ[i * 2], y, packedXZ[i * 2 + 1]));
                uv.Add(new Vector2(packedXZ[i * 2] * 0.02f, packedXZ[i * 2 + 1] * 0.02f));
            }
            var indices = new List<int>(triangles.Length);
            for (var i = 0; i < triangles.Length; i += 3)
            {
                indices.Add(triangles[i]);
                indices.Add(triangles[i + 2]);
                indices.Add(triangles[i + 1]);
            }
            return Build(vertices, indices, uv);
        }

        public static Mesh Ribbon(float[] packedXZ, float width, float y)
        {
            var count = UniqueCount(packedXZ);
            if (count < 2 || width <= 0.05f) return null;
            var vertices = new List<Vector3>(count * 2);
            var uv = new List<Vector2>(count * 2);
            var indices = new List<int>((count - 1) * 6);
            float travelled = 0f;
            for (var i = 0; i < count; i++)
            {
                var current = new Vector2(packedXZ[i * 2], packedXZ[i * 2 + 1]);
                Vector2 direction;
                if (i == 0) direction = new Vector2(packedXZ[2] - current.x, packedXZ[3] - current.y);
                else if (i == count - 1) direction = current - new Vector2(packedXZ[(i - 1) * 2], packedXZ[(i - 1) * 2 + 1]);
                else direction = new Vector2(packedXZ[(i + 1) * 2] - packedXZ[(i - 1) * 2], packedXZ[(i + 1) * 2 + 1] - packedXZ[(i - 1) * 2 + 1]);
                if (direction.sqrMagnitude < 1e-6f) direction = Vector2.right;
                direction.Normalize();
                var side = new Vector2(-direction.y, direction.x) * (width * 0.5f);
                vertices.Add(new Vector3(current.x - side.x, y, current.y - side.y));
                vertices.Add(new Vector3(current.x + side.x, y, current.y + side.y));
                uv.Add(new Vector2(0f, travelled * 0.05f));
                uv.Add(new Vector2(1f, travelled * 0.05f));
                if (i > 0)
                {
                    travelled += Vector2.Distance(current, new Vector2(packedXZ[(i - 1) * 2], packedXZ[(i - 1) * 2 + 1]));
                    var start = (i - 1) * 2;
                    indices.Add(start);
                    indices.Add(start + 2);
                    indices.Add(start + 1);
                    indices.Add(start + 1);
                    indices.Add(start + 2);
                    indices.Add(start + 3);
                }
            }
            return Build(vertices, indices, uv);
        }

        public static Mesh BoundsBox(Bounds bounds)
        {
            var mesh = Box(bounds.size);
            var vertices = mesh.vertices;
            var offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            for (var i = 0; i < vertices.Length; i++) vertices[i] += offset;
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Box(Vector3 size)
        {
            var mesh = new Mesh { name = "Box" };
            var x = size.x * 0.5f;
            var y = size.y;
            var z = size.z * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z), new Vector3(x, y, -z), new Vector3(-x, y, -z),
                new Vector3(x, 0f, z), new Vector3(-x, 0f, z), new Vector3(-x, y, z), new Vector3(x, y, z)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 6, 5, 4, 7, 6,
                5, 3, 0, 5, 6, 3,
                1, 7, 4, 1, 2, 7,
                3, 7, 2, 3, 6, 7,
                5, 1, 4, 5, 0, 1
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh Build(List<Vector3> vertices, List<int> triangles, List<Vector2> uv)
        {
            if (vertices.Count == 0 || triangles.Count == 0) return null;
            var mesh = new Mesh();
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static int UniqueCount(float[] packed)
        {
            if (packed == null || packed.Length < 4) return 0;
            var count = packed.Length / 2;
            if (count >= 2 && Mathf.Abs(packed[0] - packed[packed.Length - 2]) < 0.001f && Mathf.Abs(packed[1] - packed[packed.Length - 1]) < 0.001f)
                return count - 1;
            return count;
        }
    }
}
