using System.Collections.Generic;
using Cinderbird.Core;
using UnityEngine;

namespace Cinderbird.Valley
{
    /// <summary>
    /// Builds the placeholder valley below the summit as plain mesh data: a flat-shaded low-poly floor with hills, a rim
    /// toward the far edges, a riverbed and a flattened spot for the village; the river as a ribbon that knows how far
    /// along it each point is; the rock face of the summit's cliff; and spots for the houses. Pure, so it can be tested.
    /// </summary>
    public static class ValleyBuilder
    {
        /// <summary>The floor's height at a world x/z.</summary>
        public static float HeightAt(ValleyShape shape, float x, float z)
        {
            float dx = x - shape.Center.x;
            float dz = z - shape.Center.z;
            float across = Mathf.Abs(dx) / (shape.Size.x * 0.5f);
            // Only the far side and the flanks rise: the near side meets the foot of the summit's cliff.
            float far = dz < 0f ? -dz / (shape.Size.y * 0.5f) : 0f;
            float rim = shape.RimRise * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, Mathf.Max(across, far)));

            float noise = Mathf.PerlinNoise(x * shape.NoiseScale + shape.Seed * 13.1f, z * shape.NoiseScale + shape.Seed * 7.3f);
            float hills = (noise * 2f - 1f) * shape.HillHeight;

            float toRiver = DistanceToRiver(shape, x, z, out _);
            hills *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shape.RiverWidth, shape.RiverWidth * 4f, toRiver));
            float toVillage = Vector2.Distance(new Vector2(x, z), new Vector2(shape.Village.x, shape.Village.z));
            hills *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shape.VillageRadius, shape.VillageRadius * 1.6f, toVillage));
            float bed = shape.RiverDepth * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shape.RiverWidth * 0.5f, shape.RiverWidth * 1.4f, toRiver)));

            return shape.Center.y + rim + hills - bed;
        }

        /// <summary>Horizontal distance from a point to the river's course, and how far along the river (0..1) the nearest point is.</summary>
        public static float DistanceToRiver(ValleyShape shape, float x, float z, out float along)
        {
            along = 0f;
            Vector3[] river = shape.River;
            if (river == null || river.Length < 2)
            {
                return float.MaxValue;
            }

            float total = 0f;
            for (int i = 1; i < river.Length; i++)
            {
                total += Flat(river[i] - river[i - 1]).magnitude;
            }

            float best = float.MaxValue;
            float walked = 0f;
            Vector2 point = new Vector2(x, z);
            for (int i = 1; i < river.Length; i++)
            {
                Vector2 a = new Vector2(river[i - 1].x, river[i - 1].z);
                Vector2 b = new Vector2(river[i].x, river[i].z);
                Vector2 segment = b - a;
                float length = segment.magnitude;
                float t = length > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / (length * length)) : 0f;
                float distance = Vector2.Distance(point, a + segment * t);
                if (distance < best)
                {
                    best = distance;
                    along = total > 0f ? (walked + length * t) / total : 0f;
                }

                walked += length;
            }

            return best;
        }

        /// <summary>The valley floor: one flat-shaded triangle at a time (three vertices each), all in <paramref name="color"/>.</summary>
        public static MeshData BuildGround(ValleyShape shape, Color color)
        {
            int cells = shape.Cells;
            float stepX = shape.Size.x / cells;
            float stepZ = shape.Size.y / cells;
            float x0 = shape.Center.x - shape.Size.x * 0.5f;
            float z0 = shape.Center.z - shape.Size.y * 0.5f;
            Vector3[] grid = new Vector3[(cells + 1) * (cells + 1)];
            for (int j = 0; j <= cells; j++)
            {
                for (int i = 0; i <= cells; i++)
                {
                    float x = x0 + i * stepX;
                    float z = z0 + j * stepZ;
                    grid[i + j * (cells + 1)] = new Vector3(x, HeightAt(shape, x, z), z);
                }
            }

            List<Vector3> vertices = new List<Vector3>(cells * cells * 6);
            for (int j = 0; j < cells; j++)
            {
                for (int i = 0; i < cells; i++)
                {
                    Vector3 a = grid[i + j * (cells + 1)];
                    Vector3 b = grid[i + 1 + j * (cells + 1)];
                    Vector3 c = grid[i + (j + 1) * (cells + 1)];
                    Vector3 d = grid[i + 1 + (j + 1) * (cells + 1)];
                    AddTriangle(vertices, a, c, b, Vector3.up);
                    AddTriangle(vertices, b, c, d, Vector3.up);
                }
            }

            return Flat(vertices, color, null);
        }

        /// <summary>The river as a ribbon a little below its banks; each vertex's uv.x says how far along the river (0..1) it is.</summary>
        public static MeshData BuildRiver(ValleyShape shape, Color color, float stepLength = 4f)
        {
            List<Vector3> path = new List<Vector3>();
            List<float> along = new List<float>();
            Vector3[] river = shape.River;
            float total = 0f;
            for (int i = 1; i < river.Length; i++)
            {
                total += Flat(river[i] - river[i - 1]).magnitude;
            }

            float walked = 0f;
            for (int i = 1; i < river.Length; i++)
            {
                Vector3 a = Flat(river[i - 1]);
                Vector3 b = Flat(river[i]);
                float length = (b - a).magnitude;
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / stepLength));
                for (int s = i == 1 ? 0 : 1; s <= steps; s++)
                {
                    Vector3 point = Vector3.Lerp(a, b, (float)s / steps);
                    path.Add(point);
                    along.Add((walked + length * s / steps) / total);
                }

                walked += length;
            }

            int count = path.Count;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uv = new Vector2[count * 2];
            Color[] colors = new Color[count * 2];
            float half = shape.RiverWidth * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Vector3 forward = Flat(path[Mathf.Min(i + 1, count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = new Vector3(-forward.z, 0f, forward.x) * half;
                float y = shape.Center.y + (HeightAt(shape, path[i].x, path[i].z) - shape.Center.y) + shape.RiverDepth * 0.55f;
                vertices[i * 2] = new Vector3(path[i].x, y, path[i].z) + side;
                vertices[i * 2 + 1] = new Vector3(path[i].x, y, path[i].z) - side;
                uv[i * 2] = uv[i * 2 + 1] = new Vector2(along[i], 0f);
                colors[i * 2] = colors[i * 2 + 1] = color;
            }

            List<int> triangles = new List<int>((count - 1) * 6);
            for (int i = 0; i < count - 1; i++)
            {
                AddFacing(triangles, vertices, i * 2, i * 2 + 1, i * 2 + 2, Vector3.up);
                AddFacing(triangles, vertices, i * 2 + 1, i * 2 + 3, i * 2 + 2, Vector3.up);
            }

            return new MeshData(vertices, triangles.ToArray(), colors, uv);
        }

        /// <summary>
        /// The summit's rock face: from the floor's edge at <paramref name="z"/> down to <paramref name="bottomY"/>, leaning
        /// out a little as it goes down, jagged, flat-shaded and facing the valley (-z).
        /// </summary>
        public static MeshData BuildCliff(float xMin, float xMax, float z, float topY, float bottomY, int seed, Color color)
        {
            const float StepX = 3f;
            const float StepY = 4f;
            int columns = Mathf.Max(1, Mathf.CeilToInt((xMax - xMin) / StepX));
            int rows = Mathf.Max(1, Mathf.CeilToInt((topY - bottomY) / StepY));
            Vector3[] grid = new Vector3[(columns + 1) * (rows + 1)];
            for (int r = 0; r <= rows; r++)
            {
                for (int c = 0; c <= columns; c++)
                {
                    float x = xMin + c * (xMax - xMin) / columns;
                    float y = topY - r * (topY - bottomY) / rows;
                    // The top row stays on the edge; below, it juts and leans out.
                    float jag = r == 0 ? 0f : (Mathf.PerlinNoise(x * 0.21f + seed, y * 0.17f + seed) - 0.3f) * 3f;
                    float lean = (topY - y) * 0.22f;
                    grid[c + r * (columns + 1)] = new Vector3(x, y, z - lean - jag);
                }
            }

            List<Vector3> vertices = new List<Vector3>(columns * rows * 6);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    Vector3 a = grid[c + r * (columns + 1)];
                    Vector3 b = grid[c + 1 + r * (columns + 1)];
                    Vector3 d = grid[c + (r + 1) * (columns + 1)];
                    Vector3 e = grid[c + 1 + (r + 1) * (columns + 1)];
                    AddTriangle(vertices, a, b, d, Vector3.back);
                    AddTriangle(vertices, b, e, d, Vector3.back);
                }
            }

            return Flat(vertices, color, seed);
        }

        /// <summary>Spots for the houses around the village's middle, on the ground, each turned its own way.</summary>
        public static void HouseSpots(ValleyShape shape, int count, List<Pose> output)
        {
            output.Clear();
            System.Random random = new System.Random(shape.Seed);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.5f) / count * Mathf.PI * 2f;
                float distance = shape.VillageRadius * (0.35f + 0.55f * (float)random.NextDouble());
                float x = shape.Village.x + Mathf.Cos(angle) * distance;
                float z = shape.Village.z + Mathf.Sin(angle) * distance;
                float yaw = (float)random.NextDouble() * 360f;
                output.Add(new Pose(new Vector3(x, HeightAt(shape, x, z), z), Quaternion.Euler(0f, yaw, 0f)));
            }
        }

        // One triangle, turned so its face points toward `facing` (front faces show with any culling).
        private static void AddTriangle(List<Vector3> vertices, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                (b, c) = (c, b);
            }

            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
        }

        private static void AddFacing(List<int> triangles, Vector3[] vertices, int a, int b, int c, Vector3 facing)
        {
            if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), facing) < 0f)
            {
                (b, c) = (c, b);
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        // Unshared vertices, so each triangle shades flat (the low-poly look); with a seed, each triangle's shade varies a little.
        private static MeshData Flat(List<Vector3> vertices, Color color, int? seed)
        {
            int[] triangles = new int[vertices.Count];
            Color[] colors = new Color[vertices.Count];
            System.Random random = seed.HasValue ? new System.Random(seed.Value) : null;
            for (int i = 0; i < vertices.Count; i += 3)
            {
                Color shade = color;
                if (random != null)
                {
                    float tint = 0.85f + 0.3f * (float)random.NextDouble();
                    shade = new Color(color.r * tint, color.g * tint, color.b * tint, color.a);
                }

                for (int k = 0; k < 3; k++)
                {
                    triangles[i + k] = i + k;
                    colors[i + k] = shade;
                }
            }

            return new MeshData(vertices.ToArray(), triangles, colors);
        }

        private static Vector3 Flat(Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }
    }
}
