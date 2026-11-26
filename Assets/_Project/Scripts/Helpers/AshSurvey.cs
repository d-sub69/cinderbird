using System.Collections.Generic;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// Finds where a helper digs next: the nearest open ash of one layer, and of the spots about as near as that, the lowest
    /// (so she works from the bottom up and the ash above slides down to her). A spot needs that layer on top, open, and at
    /// least a little deep; sealed layers count as rock. Searches outward in growing squares, so a near spot costs little.
    /// Works in the terrain's local space.
    /// </summary>
    public sealed class AshSurvey
    {
        // How far each search reaches before the next, wider one (meters); the last covers the whole mountain.
        private static readonly float[] SearchRadii = { 4f, 10f, 24f, 64f };

        private readonly float _stride;
        private readonly float _minDepth;
        private readonly float _nearBand;
        // One entry per column found; reused between searches.
        private float[] _distances = new float[0];
        private Vector3[] _points = new Vector3[0];
        private int[] _keys = new int[0];
        // Which terrain chunks hold any ash, by chunk; refreshed for chunks whose version changed. Columns over empty
        // chunks are skipped, the rest scanned from their highest chunk with ash, so air costs next to nothing.
        private bool[] _chunkHasAsh = new bool[0];
        private int[] _chunkVersions = new int[0];

        /// <param name="stride">Meters between surveyed columns.</param>
        /// <param name="minDepth">Open ash thinner than this (meters) isn't worth a scoop.</param>
        /// <param name="nearBand">Spots this much farther than the nearest still count as near; the lowest of them wins.</param>
        public AshSurvey(float stride, float minDepth, float nearBand)
        {
            _stride = Mathf.Max(0.1f, stride);
            _minDepth = Mathf.Max(0f, minDepth);
            _nearBand = Mathf.Max(0f, nearBand);
        }

        /// <summary>
        /// Whether the sample column nearest a local x/z is topped by open ash at least <paramref name="minDepth"/> deep.
        /// </summary>
        /// <param name="top">Height of its surface (local).</param>
        /// <param name="columnKey">Identifies the column, e.g. to remember spots that couldn't be reached.</param>
        /// <param name="layer">The layer on top (also when it is sealed); -1 for no ash.</param>
        public static bool TryOpenAsh(VoxelWorld world, LayerLocks locks, float x, float z, float minDepth, out float top, out int columnKey,
            out int layer)
        {
            ColumnOf(world, x, z, out int ix, out int iz);
            columnKey = ix + iz * world.SamplesX;
            return ScanColumn(world, locks, ix, iz, world.SamplesY - 2, minDepth, out top, out layer);
        }

        private static void ColumnOf(VoxelWorld world, float x, float z, out int ix, out int iz)
        {
            float cell = world.CellSize;
            ix = Mathf.Clamp(Mathf.RoundToInt((x - world.Origin.x) / cell), 1, world.SamplesX - 2);
            iz = Mathf.Clamp(Mathf.RoundToInt((z - world.Origin.z) / cell), 1, world.SamplesZ - 2);
        }

        // Down a sample column from `startY`: the first ash, whether its layer is open, and how deep the open ash goes.
        private static bool ScanColumn(VoxelWorld world, LayerLocks locks, int ix, int iz, int startY, float minDepth, out float top,
            out int layer)
        {
            float cell = world.CellSize;
            top = world.Origin.y;
            layer = -1;
            float above = world.GetDensity(ix, Mathf.Min(startY + 1, world.SamplesY - 1), iz);
            for (int iy = startY; iy >= 1; iy--)
            {
                float density = world.GetDensity(ix, iy, iz);
                if (density <= 0f)
                {
                    above = density;
                    continue;
                }

                layer = world.GetLayer(ix, iy, iz);
                // The density ramps linearly across the surface: interpolate where it crosses 0.
                float t = density / (density - Mathf.Min(above, 0f));
                top = world.Origin.y + (iy + t) * cell;
                if (locks != null && !locks.IsUnlocked(layer))
                {
                    return false;
                }

                // Down through the open ash to where it ends (air, a sealed layer or the floor).
                int bottom = iy;
                while (bottom - 1 >= 1 && world.GetDensity(ix, bottom - 1, iz) > 0f &&
                       (locks == null || locks.IsUnlocked(world.GetLayer(ix, bottom - 1, iz))))
                {
                    bottom--;
                }

                float bottomY = world.Origin.y + (bottom - 0.5f) * cell;
                return top - bottomY >= minDepth;
            }

            return false;
        }

        /// <summary>
        /// The next spot to dig: the nearest open ash of <paramref name="layer"/> to <paramref name="from"/>, and of the spots
        /// within the near band of that, the lowest. False if that layer has no open ash left anywhere (that isn't excluded).
        /// </summary>
        /// <param name="from">Where she is (local).</param>
        /// <param name="excluded">Optional: column keys to skip (spots found unreachable).</param>
        /// <param name="surface">The spot's surface point (local).</param>
        public bool TryFindNearest(VoxelWorld world, LayerLocks locks, Vector3 from, int layer, ICollection<int> excluded, out Vector3 surface,
            out int columnKey)
        {
            RefreshChunkIndex(world);
            surface = default;
            columnKey = -1;
            float searched = -1f;
            float farthest = FarthestCorner(world, from);
            foreach (float radius in SearchRadii)
            {
                if (Search(world, locks, from, layer, searched, radius, excluded, out surface, out columnKey))
                {
                    return true;
                }

                // Already covered the whole grid: a wider search would only look at nothing.
                if (radius >= farthest)
                {
                    break;
                }

                searched = radius;
            }

            return false;
        }

        /// <summary>
        /// The open spot of a layer closest to a point inside the ash (a cluster's middle), within <paramref name="radius"/>
        /// of it across: where to dig to reach it. Not the lowest spot: on a slope that would be downhill of the point,
        /// and digging there would never reach it.
        /// </summary>
        public bool TryFindClosestTo(VoxelWorld world, LayerLocks locks, Vector3 target, float radius, int layer,
            ICollection<int> excluded, out Vector3 surface, out int columnKey)
        {
            RefreshChunkIndex(world);
            surface = default;
            columnKey = -1;
            int steps = Mathf.CeilToInt(radius / _stride);
            float best = float.MaxValue;
            for (int j = -steps; j <= steps; j++)
            {
                for (int i = -steps; i <= steps; i++)
                {
                    float x = target.x + i * _stride;
                    float z = target.z + j * _stride;
                    if ((i * i + j * j) * _stride * _stride > radius * radius || !IsInside(world, x, z) ||
                        !TryOpenAshIndexed(world, locks, x, z, out float top, out int key, out int topLayer) ||
                        topLayer != layer || (excluded != null && excluded.Contains(key)))
                    {
                        continue;
                    }

                    Vector3 point = new Vector3(x, top, z);
                    float distance = (point - target).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        surface = point;
                        columnKey = key;
                    }
                }
            }

            return columnKey >= 0;
        }

        /// <summary>Whether a layer has open ash anywhere (that isn't excluded).</summary>
        public bool HasOpenAsh(VoxelWorld world, LayerLocks locks, Vector3 from, int layer, ICollection<int> excluded)
        {
            return TryFindNearest(world, locks, from, layer, excluded, out _, out _);
        }

        // One square around `from`, out to `radius`; columns within `inner` were looked at by a smaller search already.
        private bool Search(VoxelWorld world, LayerLocks locks, Vector3 from, int layer, float inner, float radius, ICollection<int> excluded,
            out Vector3 surface, out int columnKey)
        {
            surface = default;
            columnKey = -1;
            int steps = Mathf.CeilToInt(radius / _stride);
            // Only the part of the square inside the grid: outside it there is no ash to find.
            int iMin = Mathf.Max(-steps, Mathf.CeilToInt((world.Origin.x - from.x) / _stride));
            int iMax = Mathf.Min(steps, Mathf.FloorToInt((world.Origin.x + world.Size.x - from.x) / _stride));
            int jMin = Mathf.Max(-steps, Mathf.CeilToInt((world.Origin.z - from.z) / _stride));
            int jMax = Mathf.Min(steps, Mathf.FloorToInt((world.Origin.z + world.Size.z - from.z) / _stride));
            EnsureCapacity(64);
            int count = 0;
            float nearest = float.MaxValue;
            for (int j = jMin; j <= jMax; j++)
            {
                for (int i = iMin; i <= iMax; i++)
                {
                    float x = from.x + i * _stride;
                    float z = from.z + j * _stride;
                    float distance = Mathf.Sqrt((i * i + j * j) * _stride * _stride);
                    if (distance > radius || distance <= inner || !IsInside(world, x, z) ||
                        !TryOpenAshIndexed(world, locks, x, z, out float top, out int key, out int topLayer) ||
                        topLayer != layer || (excluded != null && excluded.Contains(key)))
                    {
                        continue;
                    }

                    if (count >= _distances.Length)
                    {
                        EnsureCapacity(_distances.Length * 2);
                    }

                    _distances[count] = distance;
                    _points[count] = new Vector3(x, top, z);
                    _keys[count] = key;
                    count++;
                    nearest = Mathf.Min(nearest, distance);
                }
            }

            // Of the spots about as near as the nearest: the lowest, so the ash above slides down to her.
            float lowest = float.MaxValue;
            for (int k = 0; k < count; k++)
            {
                if (_distances[k] <= nearest + _nearBand && _points[k].y < lowest)
                {
                    lowest = _points[k].y;
                    surface = _points[k];
                    columnKey = _keys[k];
                }
            }

            return columnKey >= 0;
        }

        private bool TryOpenAshIndexed(VoxelWorld world, LayerLocks locks, float x, float z, out float top, out int columnKey, out int layer)
        {
            ColumnOf(world, x, z, out int ix, out int iz);
            columnKey = ix + iz * world.SamplesX;
            top = world.Origin.y;
            layer = -1;
            int startY = HighestAshSample(world, ix, iz);
            return startY >= 1 && ScanColumn(world, locks, ix, iz, startY, _minDepth, out top, out layer);
        }

        // The top sample of the highest chunk with ash over a sample column; 0 when none has any. A sample on a chunk border
        // belongs to both chunks beside it, so both are asked.
        private int HighestAshSample(VoxelWorld world, int ix, int iz)
        {
            int cells = world.ChunkCells;
            int x0 = Mathf.Min((ix - 1) / cells, world.ChunksX - 1);
            int x1 = Mathf.Min(ix / cells, world.ChunksX - 1);
            int z0 = Mathf.Min((iz - 1) / cells, world.ChunksZ - 1);
            int z1 = Mathf.Min(iz / cells, world.ChunksZ - 1);
            for (int cy = world.ChunksY - 1; cy >= 0; cy--)
            {
                if (_chunkHasAsh[world.ChunkIndex(x0, cy, z0)] || _chunkHasAsh[world.ChunkIndex(x1, cy, z0)] ||
                    _chunkHasAsh[world.ChunkIndex(x0, cy, z1)] || _chunkHasAsh[world.ChunkIndex(x1, cy, z1)])
                {
                    return Mathf.Min(world.SamplesY - 2, (cy + 1) * cells);
                }
            }

            return 0;
        }

        private void RefreshChunkIndex(VoxelWorld world)
        {
            if (_chunkHasAsh.Length != world.ChunkCount)
            {
                _chunkHasAsh = new bool[world.ChunkCount];
                _chunkVersions = new int[world.ChunkCount];
                for (int i = 0; i < _chunkVersions.Length; i++)
                {
                    _chunkVersions[i] = int.MinValue;
                }
            }

            for (int i = 0; i < world.ChunkCount; i++)
            {
                int version = world.VersionOf(i);
                if (version == _chunkVersions[i])
                {
                    continue;
                }

                _chunkVersions[i] = version;
                float[] densities = world.GetChunk(i).Densities;
                bool hasAsh = false;
                for (int s = 0; s < densities.Length && !hasAsh; s++)
                {
                    hasAsh = densities[s] > 0f;
                }

                _chunkHasAsh[i] = hasAsh;
            }
        }

        private static float FarthestCorner(VoxelWorld world, Vector3 from)
        {
            float dx = Mathf.Max(Mathf.Abs(from.x - world.Origin.x), Mathf.Abs(world.Origin.x + world.Size.x - from.x));
            float dz = Mathf.Max(Mathf.Abs(from.z - world.Origin.z), Mathf.Abs(world.Origin.z + world.Size.z - from.z));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // Outside the grid a column lookup would clamp to its edge and find ash that isn't there.
        private static bool IsInside(VoxelWorld world, float x, float z)
        {
            return x > world.Origin.x && z > world.Origin.z && x < world.Origin.x + world.Size.x && z < world.Origin.z + world.Size.z;
        }

        // Grows only, keeping what is there.
        private void EnsureCapacity(int count)
        {
            if (_distances.Length >= count)
            {
                return;
            }

            System.Array.Resize(ref _distances, count);
            System.Array.Resize(ref _points, count);
            System.Array.Resize(ref _keys, count);
        }
    }
}
