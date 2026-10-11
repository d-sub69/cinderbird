using System;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Builds a triangle mesh from a <see cref="VoxelGrid"/>. Vertices are shared between neighboring cells,
    /// giving a closed, smooth-shaded mesh. Owns a reusable edge cache, so one instance per chunk.
    /// </summary>
    public sealed class MarchingCubes
    {
        private const float IsoLevel = 0f;
        private const int AxisCount = 3;

        // Per grid edge (sample index * 3 + axis): index of the vertex already created on it, or -1.
        private int[] _edgeVertexCache = Array.Empty<int>();
        private readonly float[] _cornerDensities = new float[8];

        public void Polygonize(VoxelGrid grid, MeshBuffers output)
        {
            output.Clear();
            PrepareEdgeCache(grid);

            float[] densities = grid.Densities;
            int[] triangles = MarchingCubesTables.Triangles;
            Vector3Int[] cornerOffsets = MarchingCubesTables.CornerOffsets;

            for (int z = 0; z < grid.CellsZ; z++)
            {
                for (int y = 0; y < grid.CellsY; y++)
                {
                    for (int x = 0; x < grid.CellsX; x++)
                    {
                        int caseIndex = 0;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            Vector3Int offset = cornerOffsets[corner];
                            float density = densities[grid.Index(x + offset.x, y + offset.y, z + offset.z)];
                            _cornerDensities[corner] = density;
                            // Bit set = ash. With the table's winding this makes triangles face the air.
                            if (density > IsoLevel)
                            {
                                caseIndex |= 1 << corner;
                            }
                        }

                        if (caseIndex == 0 || caseIndex == 255)
                        {
                            continue;
                        }

                        int row = caseIndex * MarchingCubesTables.TriangleRowStride;
                        for (int i = row; triangles[i] != -1; i++)
                        {
                            output.Indices.Add(GetOrCreateEdgeVertex(grid, x, y, z, triangles[i], output));
                        }
                    }
                }
            }
        }

        private void PrepareEdgeCache(VoxelGrid grid)
        {
            int size = grid.SamplesX * grid.SamplesY * grid.SamplesZ * AxisCount;
            if (_edgeVertexCache.Length != size)
            {
                _edgeVertexCache = new int[size];
            }

            Array.Fill(_edgeVertexCache, -1);
        }

        private int GetOrCreateEdgeVertex(VoxelGrid grid, int cellX, int cellY, int cellZ, int edge, MeshBuffers output)
        {
            int cornerA = MarchingCubesTables.EdgeCorners[edge * 2];
            int cornerB = MarchingCubesTables.EdgeCorners[edge * 2 + 1];
            Vector3Int offsetA = MarchingCubesTables.CornerOffsets[cornerA];
            Vector3Int offsetB = MarchingCubesTables.CornerOffsets[cornerB];

            // Key the edge by its lower sample and axis, so neighboring cells find the same vertex.
            Vector3Int lower = Vector3Int.Min(offsetA, offsetB);
            Vector3Int axisStep = offsetA - offsetB;
            int axis = axisStep.x != 0 ? 0 : axisStep.y != 0 ? 1 : 2;

            int x = cellX + lower.x;
            int y = cellY + lower.y;
            int z = cellZ + lower.z;
            int key = grid.Index(x, y, z) * AxisCount + axis;

            int cached = _edgeVertexCache[key];
            if (cached != -1)
            {
                return cached;
            }

            int x2 = axis == 0 ? x + 1 : x;
            int y2 = axis == 1 ? y + 1 : y;
            int z2 = axis == 2 ? z + 1 : z;

            float densityA = grid.GetDensity(x, y, z);
            float densityB = grid.GetDensity(x2, y2, z2);
            float t = (IsoLevel - densityA) / (densityB - densityA);

            Vector3 position = Vector3.Lerp(grid.SamplePosition(x, y, z), grid.SamplePosition(x2, y2, z2), t);
            Vector3 gradient = Vector3.Lerp(Gradient(grid, x, y, z), Gradient(grid, x2, y2, z2), t);
            // Density rises into the ash, so the outward normal points down the gradient.
            Vector3 normal = gradient.sqrMagnitude > 0f ? -gradient.normalized : Vector3.up;

            int vertexIndex = output.Vertices.Count;
            output.Vertices.Add(position);
            output.Normals.Add(normal);
            _edgeVertexCache[key] = vertexIndex;
            return vertexIndex;
        }

        private static Vector3 Gradient(VoxelGrid grid, int x, int y, int z)
        {
            int maxX = grid.SamplesX - 1;
            int maxY = grid.SamplesY - 1;
            int maxZ = grid.SamplesZ - 1;

            float dx = grid.GetDensity(Math.Min(x + 1, maxX), y, z) - grid.GetDensity(Math.Max(x - 1, 0), y, z);
            float dy = grid.GetDensity(x, Math.Min(y + 1, maxY), z) - grid.GetDensity(x, Math.Max(y - 1, 0), z);
            float dz = grid.GetDensity(x, y, Math.Min(z + 1, maxZ)) - grid.GetDensity(x, y, Math.Max(z - 1, 0));
            return new Vector3(dx, dy, dz);
        }
    }
}
