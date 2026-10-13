using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// The whole diggable terrain as a block of chunks, each its own <see cref="VoxelGrid"/>. Neighboring
    /// chunks share their border samples (a chunk of N cells has N + 1 samples per axis, and the last one is
    /// also the next chunk's first), so their meshes meet without cracks. Writes update every copy of a sample
    /// and mark the affected chunks dirty for re-meshing and modified for saving. Addressed by global sample index.
    /// </summary>
    public sealed class VoxelWorld : IDensityField
    {
        private readonly VoxelGrid[] _chunks;
        private readonly bool[] _dirty;
        // Separate from _dirty: re-meshing clears dirty flags after every dig, but a save needs every chunk
        // changed since the mountain was generated.
        private readonly bool[] _modified;

        public VoxelWorld(int chunkCells, int chunksX, int chunksY, int chunksZ, float cellSize, Vector3 origin)
        {
            if (chunkCells < 1 || chunksX < 1 || chunksY < 1 || chunksZ < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkCells), "The world needs at least one chunk of one cell.");
            }

            ChunkCells = chunkCells;
            ChunksX = chunksX;
            ChunksY = chunksY;
            ChunksZ = chunksZ;
            CellSize = cellSize;
            Origin = origin;

            _chunks = new VoxelGrid[chunksX * chunksY * chunksZ];
            _dirty = new bool[_chunks.Length];
            _modified = new bool[_chunks.Length];
            for (int z = 0; z < chunksZ; z++)
            {
                for (int y = 0; y < chunksY; y++)
                {
                    for (int x = 0; x < chunksX; x++)
                    {
                        Vector3 chunkOrigin = origin + new Vector3(x, y, z) * (chunkCells * cellSize);
                        _chunks[ChunkIndex(x, y, z)] = new VoxelGrid(chunkCells, chunkCells, chunkCells, cellSize, chunkOrigin);
                    }
                }
            }
        }

        /// <summary>Cells along each edge of a chunk.</summary>
        public int ChunkCells { get; }

        public int ChunksX { get; }
        public int ChunksY { get; }
        public int ChunksZ { get; }
        public int ChunkCount => _chunks.Length;

        public int SamplesX => ChunksX * ChunkCells + 1;
        public int SamplesY => ChunksY * ChunkCells + 1;
        public int SamplesZ => ChunksZ * ChunkCells + 1;
        public float CellSize { get; }
        public float CellVolume => CellSize * CellSize * CellSize;
        public Vector3 Origin { get; }

        /// <summary>Size of the whole world in local meters.</summary>
        public Vector3 Size => new Vector3(ChunksX, ChunksY, ChunksZ) * (ChunkCells * CellSize);

        public int ChunkIndex(int x, int y, int z)
        {
            return x + ChunksX * (y + ChunksY * z);
        }

        public Vector3Int ChunkCoord(int index)
        {
            int x = index % ChunksX;
            int y = index / ChunksX % ChunksY;
            int z = index / (ChunksX * ChunksY);
            return new Vector3Int(x, y, z);
        }

        public VoxelGrid GetChunk(int index)
        {
            return _chunks[index];
        }

        /// <summary>Global index of a chunk's first sample.</summary>
        public Vector3Int ChunkSampleOffset(int index)
        {
            return ChunkCoord(index) * ChunkCells;
        }

        public float GetDensity(int x, int y, int z)
        {
            int cx = Math.Min(x / ChunkCells, ChunksX - 1);
            int cy = Math.Min(y / ChunkCells, ChunksY - 1);
            int cz = Math.Min(z / ChunkCells, ChunksZ - 1);
            return _chunks[ChunkIndex(cx, cy, cz)].GetDensity(x - cx * ChunkCells, y - cy * ChunkCells, z - cz * ChunkCells);
        }

        /// <summary>Writes the sample into every chunk that holds a copy (up to 8 at a chunk corner).</summary>
        public void SetDensity(int x, int y, int z, float density)
        {
            int countX = ChunksHolding(x, ChunksX, out int firstX);
            int countY = ChunksHolding(y, ChunksY, out int firstY);
            int countZ = ChunksHolding(z, ChunksZ, out int firstZ);

            for (int cz = firstZ; cz < firstZ + countZ; cz++)
            {
                for (int cy = firstY; cy < firstY + countY; cy++)
                {
                    for (int cx = firstX; cx < firstX + countX; cx++)
                    {
                        int index = ChunkIndex(cx, cy, cz);
                        _chunks[index].SetDensity(x - cx * ChunkCells, y - cy * ChunkCells, z - cz * ChunkCells, density);
                        _dirty[index] = true;
                        _modified[index] = true;
                    }
                }
            }
        }

        public byte GetLayer(int x, int y, int z)
        {
            int cx = Math.Min(x / ChunkCells, ChunksX - 1);
            int cy = Math.Min(y / ChunkCells, ChunksY - 1);
            int cz = Math.Min(z / ChunkCells, ChunksZ - 1);
            return _chunks[ChunkIndex(cx, cy, cz)].GetLayer(x - cx * ChunkCells, y - cy * ChunkCells, z - cz * ChunkCells);
        }

        /// <summary>Writes the layer into every chunk that holds a copy of the sample, like <see cref="SetDensity"/>.</summary>
        public void SetLayer(int x, int y, int z, byte layer)
        {
            int countX = ChunksHolding(x, ChunksX, out int firstX);
            int countY = ChunksHolding(y, ChunksY, out int firstY);
            int countZ = ChunksHolding(z, ChunksZ, out int firstZ);

            for (int cz = firstZ; cz < firstZ + countZ; cz++)
            {
                for (int cy = firstY; cy < firstY + countY; cy++)
                {
                    for (int cx = firstX; cx < firstX + countX; cx++)
                    {
                        int index = ChunkIndex(cx, cy, cz);
                        _chunks[index].SetLayer(x - cx * ChunkCells, y - cy * ChunkCells, z - cz * ChunkCells, layer);
                        _dirty[index] = true;
                        _modified[index] = true;
                    }
                }
            }
        }

        /// <summary>Index of the chunk containing a local position, clamped to the world.</summary>
        public int ChunkIndexAt(Vector3 localPosition)
        {
            float chunkSize = ChunkCells * CellSize;
            Vector3 relative = localPosition - Origin;
            int x = Mathf.Clamp(Mathf.FloorToInt(relative.x / chunkSize), 0, ChunksX - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(relative.y / chunkSize), 0, ChunksY - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(relative.z / chunkSize), 0, ChunksZ - 1);
            return ChunkIndex(x, y, z);
        }

        /// <summary>Only the world's outer border; chunk seams are ordinary samples.</summary>
        public bool IsBoundary(int x, int y, int z)
        {
            return x == 0 || y == 0 || z == 0 || x == SamplesX - 1 || y == SamplesY - 1 || z == SamplesZ - 1;
        }

        public Vector3 SamplePosition(int x, int y, int z)
        {
            return Origin + new Vector3(x, y, z) * CellSize;
        }

        public float SampleDensity(Vector3 localPosition)
        {
            Vector3 cell = (localPosition - Origin) / CellSize;
            int maxX = SamplesX - 1;
            int maxY = SamplesY - 1;
            int maxZ = SamplesZ - 1;
            float fx = Mathf.Clamp(cell.x, 0f, maxX);
            float fy = Mathf.Clamp(cell.y, 0f, maxY);
            float fz = Mathf.Clamp(cell.z, 0f, maxZ);

            int x0 = Mathf.Min(Mathf.FloorToInt(fx), maxX - 1);
            int y0 = Mathf.Min(Mathf.FloorToInt(fy), maxY - 1);
            int z0 = Mathf.Min(Mathf.FloorToInt(fz), maxZ - 1);
            float tx = fx - x0;
            float ty = fy - y0;
            float tz = fz - z0;

            float d00 = Mathf.Lerp(GetDensity(x0, y0, z0), GetDensity(x0 + 1, y0, z0), tx);
            float d10 = Mathf.Lerp(GetDensity(x0, y0 + 1, z0), GetDensity(x0 + 1, y0 + 1, z0), tx);
            float d01 = Mathf.Lerp(GetDensity(x0, y0, z0 + 1), GetDensity(x0 + 1, y0, z0 + 1), tx);
            float d11 = Mathf.Lerp(GetDensity(x0, y0 + 1, z0 + 1), GetDensity(x0 + 1, y0 + 1, z0 + 1), tx);
            return Mathf.Lerp(Mathf.Lerp(d00, d10, ty), Mathf.Lerp(d01, d11, ty), tz);
        }

        /// <summary>
        /// Total ash volume in cubic meters, counting each shared sample once. Walks every sample, so it's meant
        /// for start-up; keep a running total for per-dig updates.
        /// </summary>
        public float TotalAshVolume()
        {
            double fill = 0d;
            for (int index = 0; index < _chunks.Length; index++)
            {
                Vector3Int coord = ChunkCoord(index);
                VoxelGrid chunk = _chunks[index];
                // A chunk owns its samples up to, but not including, the shared far border, except at the world's edge.
                int endX = coord.x == ChunksX - 1 ? ChunkCells : ChunkCells - 1;
                int endY = coord.y == ChunksY - 1 ? ChunkCells : ChunkCells - 1;
                int endZ = coord.z == ChunksZ - 1 ? ChunkCells : ChunkCells - 1;
                for (int z = 0; z <= endZ; z++)
                {
                    for (int y = 0; y <= endY; y++)
                    {
                        for (int x = 0; x <= endX; x++)
                        {
                            fill += VoxelGrid.Fill(chunk.GetDensity(x, y, z));
                        }
                    }
                }
            }

            return (float)(fill * CellVolume);
        }

        /// <summary>Indices of chunks whose cells overlap a local-space box.</summary>
        public void ChunksOverlapping(Vector3 min, Vector3 max, List<int> output)
        {
            output.Clear();
            float chunkSize = ChunkCells * CellSize;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - Origin.x) / chunkSize), 0, ChunksX - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((min.y - Origin.y) / chunkSize), 0, ChunksY - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((min.z - Origin.z) / chunkSize), 0, ChunksZ - 1);
            int x1 = Mathf.Clamp(Mathf.FloorToInt((max.x - Origin.x) / chunkSize), 0, ChunksX - 1);
            int y1 = Mathf.Clamp(Mathf.FloorToInt((max.y - Origin.y) / chunkSize), 0, ChunksY - 1);
            int z1 = Mathf.Clamp(Mathf.FloorToInt((max.z - Origin.z) / chunkSize), 0, ChunksZ - 1);

            for (int z = z0; z <= z1; z++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        output.Add(ChunkIndex(x, y, z));
                    }
                }
            }
        }

        public void MarkDirty(int index)
        {
            _dirty[index] = true;
        }

        /// <summary>Whether a chunk has been written to since the last <see cref="ClearModified"/>.</summary>
        public bool IsModified(int index)
        {
            return _modified[index];
        }

        public void MarkModified(int index)
        {
            _modified[index] = true;
        }

        /// <summary>Starts counting changes afresh, e.g. right after the mountain is generated.</summary>
        public void ClearModified()
        {
            Array.Clear(_modified, 0, _modified.Length);
        }

        public void MarkAllDirty()
        {
            for (int i = 0; i < _dirty.Length; i++)
            {
                _dirty[i] = true;
            }
        }

        /// <summary>Moves the indices of chunks changed since the last call into <paramref name="output"/> and clears the flags.</summary>
        public void TakeDirtyChunks(List<int> output)
        {
            output.Clear();
            for (int i = 0; i < _dirty.Length; i++)
            {
                if (_dirty[i])
                {
                    output.Add(i);
                    _dirty[i] = false;
                }
            }
        }

        // A global sample index sits in one chunk, or in two when it lies on the seam between them.
        private int ChunksHolding(int sample, int chunkCount, out int firstChunk)
        {
            int chunk = sample / ChunkCells;
            bool onSeam = sample % ChunkCells == 0 && chunk > 0;
            if (chunk >= chunkCount)
            {
                // The world's far edge belongs only to the last chunk.
                firstChunk = chunkCount - 1;
                return 1;
            }

            firstChunk = onSeam ? chunk - 1 : chunk;
            return onSeam ? 2 : 1;
        }
    }
}
