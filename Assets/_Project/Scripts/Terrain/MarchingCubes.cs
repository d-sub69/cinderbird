using System;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Builds a triangle mesh from a <see cref="VoxelGrid"/>. Vertices are shared between neighboring cells,
    /// giving a closed, smooth-shaded mesh. Owns a reusable edge cache, so reuse one instance.
    /// </summary>
    public sealed class MarchingCubes
    {
        private const float IsoLevel = 0f;
        private const int AxisCount = 3;

        // Per grid edge (sample index * 3 + axis): index of the vertex already created on it, or -1.
        private int[] _edgeVertexCache = Array.Empty<int>();
        private readonly float[] _cornerDensities = new float[8];

        // Where normals look past the grid's edge: the grid itself, or the whole world for a chunk.
        private IDensityField _neighborhood;
        private Vector3Int _globalOffset;

        public void Polygonize(VoxelGrid grid, MeshBuffers output)
        {
            Polygonize(grid, grid, Vector3Int.zero, output);
        }

        /// <summary>
        /// Meshes one chunk of a world. Its own samples build the surface; neighboring chunks are only read for
        /// normals at the border, so lighting matches across seams.
        /// </summary>
        public void Polygonize(VoxelWorld world, int chunkIndex, MeshBuffers output)
        {
            Polygonize(world.GetChunk(chunkIndex), world, world.ChunkSampleOffset(chunkIndex), output);
        }

        private void Polygonize(VoxelGrid grid, IDensityField neighborhood, Vector3Int globalOffset, MeshBuffers output)
        {
            _neighborhood = neighborhood;
            _globalOffset = globalOffset;
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

        private Vector3 Gradient(VoxelGrid grid, int x, int y, int z)
        {
            // Inside the grid: read it directly, the fast path for almost every vertex.
            bool interior = x > 0 && y > 0 && z > 0 && x < grid.SamplesX - 1 && y < grid.SamplesY - 1 && z < grid.SamplesZ - 1;
            if (interior)
            {
                return new Vector3(
                    grid.GetDensity(x + 1, y, z) - grid.GetDensity(x - 1, y, z),
                    grid.GetDensity(x, y + 1, z) - grid.GetDensity(x, y - 1, z),
                    grid.GetDensity(x, y, z + 1) - grid.GetDensity(x, y, z - 1));
            }

            // On the grid's border: look into the neighborhood (the neighboring chunks), clamped at its own edge.
            IDensityField field = _neighborhood;
            int gx = x + _globalOffset.x;
            int gy = y + _globalOffset.y;
            int gz = z + _globalOffset.z;
            int maxX = field.SamplesX - 1;
            int maxY = field.SamplesY - 1;
            int maxZ = field.SamplesZ - 1;

            float dx = field.GetDensity(Math.Min(gx + 1, maxX), gy, gz) - field.GetDensity(Math.Max(gx - 1, 0), gy, gz);
            float dy = field.GetDensity(gx, Math.Min(gy + 1, maxY), gz) - field.GetDensity(gx, Math.Max(gy - 1, 0), gz);
            float dz = field.GetDensity(gx, gy, Math.Min(gz + 1, maxZ)) - field.GetDensity(gx, gy, Math.Max(gz - 1, 0));
            return new Vector3(dx, dy, dz);
        }
    }
}
