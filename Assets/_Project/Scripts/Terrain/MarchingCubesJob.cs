using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Marching cubes for one block of cells, Burst-compiled so many chunks can be meshed in parallel.
    /// Reads a padded copy of the densities (one extra sample on every side) so normals at the block's
    /// border come from the real neighbors, which keeps lighting seamless between chunks.
    /// Vertices are shared between neighboring cells through an edge cache, giving a closed mesh.
    /// </summary>
    [BurstCompile]
    public struct MarchingCubesJob : IJob
    {
        private const float IsoLevel = 0f;
        private const int AxisCount = 3;
        private const int TriangleRowStride = 16;

        /// <summary>(Cells + 3)³ samples: the block's (Cells + 1)³ samples plus a one-sample ring around them.</summary>
        [ReadOnly] public NativeArray<float> PaddedDensities;
        [ReadOnly] public NativeArray<int> Triangles;
        [ReadOnly] public NativeArray<int> EdgeCorners;
        [ReadOnly] public NativeArray<int3> CornerOffsets;

        /// <summary>(Cells + 1)³ layer indices, one per real sample.</summary>
        [ReadOnly] public NativeArray<byte> Layers;

        /// <summary>Vertex color per layer; layers past the end use the last entry.</summary>
        [ReadOnly] public NativeArray<float4> LayerTints;

        public int3 Cells;
        public float CellSize;

        /// <summary>Local position of the block's first real (unpadded) sample.</summary>
        public float3 Origin;

        /// <summary>Scratch space of (Cells + 1)³ × 3 entries; filled with -1 at the start.</summary>
        public NativeArray<int> EdgeCache;

        public NativeList<float3> Vertices;
        public NativeList<float3> Normals;
        public NativeList<float4> Colors;
        public NativeList<int> Indices;

        public void Execute()
        {
            Vertices.Clear();
            Normals.Clear();
            Colors.Clear();
            Indices.Clear();
            for (int i = 0; i < EdgeCache.Length; i++)
            {
                EdgeCache[i] = -1;
            }

            for (int z = 0; z < Cells.z; z++)
            {
                for (int y = 0; y < Cells.y; y++)
                {
                    for (int x = 0; x < Cells.x; x++)
                    {
                        PolygonizeCell(new int3(x, y, z));
                    }
                }
            }
        }

        private void PolygonizeCell(int3 cell)
        {
            int caseIndex = 0;
            for (int corner = 0; corner < 8; corner++)
            {
                // Bit set = ash. With the table's winding this makes triangles face the air.
                if (Density(cell + CornerOffsets[corner]) > IsoLevel)
                {
                    caseIndex |= 1 << corner;
                }
            }

            if (caseIndex == 0 || caseIndex == 255)
            {
                return;
            }

            for (int i = caseIndex * TriangleRowStride; Triangles[i] != -1; i++)
            {
                Indices.Add(GetOrCreateEdgeVertex(cell, Triangles[i]));
            }
        }

        private int GetOrCreateEdgeVertex(int3 cell, int edge)
        {
            int3 offsetA = CornerOffsets[EdgeCorners[edge * 2]];
            int3 offsetB = CornerOffsets[EdgeCorners[edge * 2 + 1]];

            // Key the edge by its lower sample and axis, so neighboring cells find the same vertex.
            int3 lower = math.min(offsetA, offsetB);
            int3 step = math.abs(offsetA - offsetB);
            int axis = step.x != 0 ? 0 : step.y != 0 ? 1 : 2;

            int3 a = cell + lower;
            int3 b = a + step;
            int key = SampleIndex(a) * AxisCount + axis;
            int cached = EdgeCache[key];
            if (cached != -1)
            {
                return cached;
            }

            float densityA = Density(a);
            float densityB = Density(b);
            float t = (IsoLevel - densityA) / (densityB - densityA);

            float3 position = Origin + math.lerp((float3)a, (float3)b, t) * CellSize;
            float3 gradient = math.lerp(Gradient(a), Gradient(b), t);
            // Density rises into the ash, so the outward normal points down the gradient.
            float3 normal = math.lengthsq(gradient) > 0f ? -math.normalize(gradient) : new float3(0f, 1f, 0f);

            // Color from the ash side of the edge: the surface shows the layer that was cut into.
            int ashSample = SampleIndex(densityA > IsoLevel ? a : b);
            int tint = math.min(Layers[ashSample], LayerTints.Length - 1);

            int vertexIndex = Vertices.Length;
            Vertices.Add(position);
            Normals.Add(normal);
            Colors.Add(LayerTints[tint]);
            EdgeCache[key] = vertexIndex;
            return vertexIndex;
        }

        // The padding ring makes x - 1 and x + 1 valid for every real sample.
        private float3 Gradient(int3 s)
        {
            return new float3(
                Density(s + new int3(1, 0, 0)) - Density(s - new int3(1, 0, 0)),
                Density(s + new int3(0, 1, 0)) - Density(s - new int3(0, 1, 0)),
                Density(s + new int3(0, 0, 1)) - Density(s - new int3(0, 0, 1)));
        }

        /// <param name="sample">Real sample coordinate; -1 and Cells + 1 reach into the padding.</param>
        private float Density(int3 sample)
        {
            int3 padded = Cells + 3;
            int3 p = sample + 1;
            return PaddedDensities[p.x + padded.x * (p.y + padded.y * p.z)];
        }

        private int SampleIndex(int3 sample)
        {
            int3 samples = Cells + 1;
            return sample.x + samples.x * (sample.y + samples.y * sample.z);
        }
    }
}
