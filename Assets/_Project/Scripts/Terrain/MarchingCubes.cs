using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Meshes voxel data with <see cref="MarchingCubesJob"/>: one chunk, a single grid, or a batch of chunks in
    /// parallel. Owns native lookup tables, so dispose it when done.
    /// </summary>
    public sealed class MarchingCubes : IDisposable
    {
        private NativeArray<int> _triangles;
        private NativeArray<int> _edgeCorners;
        private NativeArray<int3> _cornerOffsets;

        // Results of the last batch, valid until ReleaseResults or the next batch.
        private readonly List<BatchItem> _batch = new List<BatchItem>();

        private struct BatchItem
        {
            public NativeArray<float> PaddedDensities;
            public NativeArray<int> EdgeCache;
            public NativeList<float3> Vertices;
            public NativeList<float3> Normals;
            public NativeList<int> Indices;
        }

        public MarchingCubes()
        {
            _triangles = new NativeArray<int>(MarchingCubesTables.Triangles, Allocator.Persistent);
            _edgeCorners = new NativeArray<int>(MarchingCubesTables.EdgeCorners, Allocator.Persistent);
            _cornerOffsets = new NativeArray<int3>(MarchingCubesTables.CornerOffsets.Length, Allocator.Persistent);
            for (int i = 0; i < _cornerOffsets.Length; i++)
            {
                Vector3Int offset = MarchingCubesTables.CornerOffsets[i];
                _cornerOffsets[i] = new int3(offset.x, offset.y, offset.z);
            }
        }

        /// <summary>Number of chunk meshes from the last <see cref="PolygonizeChunks"/>.</summary>
        public int ResultCount => _batch.Count;

        public NativeArray<float3> ResultVertices(int result) => _batch[result].Vertices.AsArray();
        public NativeArray<float3> ResultNormals(int result) => _batch[result].Normals.AsArray();
        public NativeArray<int> ResultIndices(int result) => _batch[result].Indices.AsArray();

        /// <summary>Meshes a standalone grid (normals at its edge are clamped to it).</summary>
        public void Polygonize(VoxelGrid grid, MeshBuffers output)
        {
            int3 cells = new int3(grid.CellsX, grid.CellsY, grid.CellsZ);
            NativeArray<float> padded = PadGrid(grid, cells);
            RunSingle(padded, cells, grid.CellSize, grid.Origin, output);
        }

        /// <summary>Meshes one chunk of a world on its own (mainly for tests).</summary>
        public void Polygonize(VoxelWorld world, int chunkIndex, MeshBuffers output)
        {
            VoxelGrid chunk = world.GetChunk(chunkIndex);
            int3 cells = new int3(chunk.CellsX, chunk.CellsY, chunk.CellsZ);
            NativeArray<float> padded = PadChunk(world, chunkIndex, cells);
            RunSingle(padded, cells, chunk.CellSize, chunk.Origin, output);
        }

        /// <summary>
        /// Meshes several chunks in parallel and waits for them. Read the meshes with the Result* methods, then call
        /// <see cref="ReleaseResults"/> (or start another batch) to free them.
        /// </summary>
        public void PolygonizeChunks(VoxelWorld world, List<int> chunkIndices)
        {
            ReleaseResults();
            if (chunkIndices.Count == 0)
            {
                return;
            }

            NativeArray<JobHandle> handles = new NativeArray<JobHandle>(chunkIndices.Count, Allocator.Temp);
            for (int i = 0; i < chunkIndices.Count; i++)
            {
                VoxelGrid chunk = world.GetChunk(chunkIndices[i]);
                int3 cells = new int3(chunk.CellsX, chunk.CellsY, chunk.CellsZ);
                BatchItem item = Allocate(PadChunk(world, chunkIndices[i], cells), cells);
                _batch.Add(item);
                handles[i] = CreateJob(item, cells, chunk.CellSize, chunk.Origin).Schedule();
            }

            JobHandle.CompleteAll(handles);
            handles.Dispose();
        }

        public void ReleaseResults()
        {
            foreach (BatchItem item in _batch)
            {
                Free(item);
            }

            _batch.Clear();
        }

        public void Dispose()
        {
            ReleaseResults();
            if (_triangles.IsCreated)
            {
                _triangles.Dispose();
                _edgeCorners.Dispose();
                _cornerOffsets.Dispose();
            }
        }

        private void RunSingle(NativeArray<float> padded, int3 cells, float cellSize, Vector3 origin, MeshBuffers output)
        {
            BatchItem item = Allocate(padded, cells);
            try
            {
                CreateJob(item, cells, cellSize, origin).Schedule().Complete();
                CopyToBuffers(item, output);
            }
            finally
            {
                Free(item);
            }
        }

        private MarchingCubesJob CreateJob(BatchItem item, int3 cells, float cellSize, Vector3 origin)
        {
            return new MarchingCubesJob
            {
                PaddedDensities = item.PaddedDensities,
                Triangles = _triangles,
                EdgeCorners = _edgeCorners,
                CornerOffsets = _cornerOffsets,
                Cells = cells,
                CellSize = cellSize,
                Origin = new float3(origin.x, origin.y, origin.z),
                EdgeCache = item.EdgeCache,
                Vertices = item.Vertices,
                Normals = item.Normals,
                Indices = item.Indices,
            };
        }

        private static BatchItem Allocate(NativeArray<float> padded, int3 cells)
        {
            int3 samples = cells + 1;
            return new BatchItem
            {
                PaddedDensities = padded,
                EdgeCache = new NativeArray<int>(samples.x * samples.y * samples.z * 3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                Vertices = new NativeList<float3>(Allocator.TempJob),
                Normals = new NativeList<float3>(Allocator.TempJob),
                Indices = new NativeList<int>(Allocator.TempJob),
            };
        }

        private static void Free(BatchItem item)
        {
            item.PaddedDensities.Dispose();
            item.EdgeCache.Dispose();
            item.Vertices.Dispose();
            item.Normals.Dispose();
            item.Indices.Dispose();
        }

        private static void CopyToBuffers(BatchItem item, MeshBuffers output)
        {
            output.Clear();
            for (int i = 0; i < item.Vertices.Length; i++)
            {
                output.Vertices.Add(item.Vertices[i]);
                output.Normals.Add(item.Normals[i]);
            }

            for (int i = 0; i < item.Indices.Length; i++)
            {
                output.Indices.Add(item.Indices[i]);
            }
        }

        // A standalone grid has no neighbors: the padding ring repeats its edge samples, which gives the same
        // clamped normals as before.
        private static NativeArray<float> PadGrid(VoxelGrid grid, int3 cells)
        {
            int3 padded = cells + 3;
            NativeArray<float> result = new NativeArray<float>(padded.x * padded.y * padded.z, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            int index = 0;
            for (int z = -1; z <= cells.z + 1; z++)
            {
                for (int y = -1; y <= cells.y + 1; y++)
                {
                    for (int x = -1; x <= cells.x + 1; x++)
                    {
                        result[index++] = grid.GetDensity(
                            Math.Clamp(x, 0, cells.x), Math.Clamp(y, 0, cells.y), Math.Clamp(z, 0, cells.z));
                    }
                }
            }

            return result;
        }

        // A chunk's own samples come straight from its grid (fast); only the padding ring is read from the world,
        // clamped at the world's edge.
        private static NativeArray<float> PadChunk(VoxelWorld world, int chunkIndex, int3 cells)
        {
            VoxelGrid chunk = world.GetChunk(chunkIndex);
            Vector3Int offset = world.ChunkSampleOffset(chunkIndex);
            int maxX = world.SamplesX - 1;
            int maxY = world.SamplesY - 1;
            int maxZ = world.SamplesZ - 1;

            int3 padded = cells + 3;
            NativeArray<float> result = new NativeArray<float>(padded.x * padded.y * padded.z, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            float[] own = chunk.Densities;
            int index = 0;
            for (int z = -1; z <= cells.z + 1; z++)
            {
                bool zInside = z >= 0 && z <= cells.z;
                for (int y = -1; y <= cells.y + 1; y++)
                {
                    bool yzInside = zInside && y >= 0 && y <= cells.y;
                    for (int x = -1; x <= cells.x + 1; x++)
                    {
                        if (yzInside && x >= 0 && x <= cells.x)
                        {
                            result[index++] = own[chunk.Index(x, y, z)];
                            continue;
                        }

                        result[index++] = world.GetDensity(
                            Math.Clamp(x + offset.x, 0, maxX),
                            Math.Clamp(y + offset.y, 0, maxY),
                            Math.Clamp(z + offset.z, 0, maxZ));
                    }
                }
            }

            return result;
        }
    }
}
