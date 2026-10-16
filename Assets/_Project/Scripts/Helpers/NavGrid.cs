using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// The yard as a helper walks it, in world space: a grid of cells, each with the height to stand at and whether it
    /// is free. The fixed scenery (nest, stalls, walls) is filled in once (<see cref="SetStatic"/>); the ash comes live
    /// from the voxels, re-read only where chunks changed since the last refresh, so a fresh hole or a slide is known at
    /// once without re-reading the whole mountain. Things the walker bumped into can be blocked for a while.
    /// The terrain must not be rotated or scaled.
    /// </summary>
    public sealed class NavGrid : INavGrid
    {
        private readonly float[] _ground;
        private readonly bool[] _blocked;
        private readonly float[] _height;
        private readonly float[] _blockedUntil;
        private readonly float _maxStep;
        private readonly float _maxJump;
        private readonly float _maxDrop;
        private VoxelWorld _world;
        private Vector3 _terrainOffset;
        private float _minAshThickness;
        private int[] _seenVersions;
        private bool _terrainDirty;

        /// <param name="min">World x/z of the grid's first cell corner.</param>
        /// <param name="maxStep">Highest climb (meters) from one cell to the next straight neighbor, walking.</param>
        /// <param name="maxJump">Highest rise she can jump up onto a ledge (meters); 0 for no jumping.</param>
        /// <param name="maxDrop">Deepest drop she steps down off a ledge (meters); never less than a walking step.</param>
        public NavGrid(Vector2 min, int width, int depth, float cellSize, float maxStep, float maxJump = 0f, float maxDrop = 0f)
        {
            Min = min;
            Width = Mathf.Max(1, width);
            Depth = Mathf.Max(1, depth);
            CellSize = Mathf.Max(0.05f, cellSize);
            _maxStep = Mathf.Max(0f, maxStep);
            _maxJump = Mathf.Max(0f, maxJump);
            _maxDrop = Mathf.Max(0f, maxDrop);
            int count = Width * Depth;
            _ground = new float[count];
            _blocked = new bool[count];
            _height = new float[count];
            _blockedUntil = new float[count];
        }

        public Vector2 Min { get; }

        public int Width { get; }

        public int Depth { get; }

        public float CellSize { get; }

        /// <summary>The time things blocked for a while are compared against (set every frame by the owner).</summary>
        public float Now { get; set; }

        /// <summary>The fixed scenery in a cell: blocked by an obstacle, or free with the ground at a height.</summary>
        public void SetStatic(int x, int z, bool blocked, float groundHeight)
        {
            int index = x + z * Width;
            _blocked[index] = blocked;
            _ground[index] = groundHeight;
            _height[index] = groundHeight;
            _terrainDirty = true;
        }

        /// <param name="terrainOffset">World position of the terrain's origin (its transform).</param>
        /// <param name="minAshThickness">Ash thinner than this over the ground counts as ground.</param>
        public void AttachTerrain(VoxelWorld world, Vector3 terrainOffset, float minAshThickness)
        {
            _world = world;
            _terrainOffset = terrainOffset;
            _minAshThickness = minAshThickness;
            _seenVersions = new int[world.ChunkCount];
            for (int i = 0; i < _seenVersions.Length; i++)
            {
                _seenVersions[i] = -1;
            }

            _terrainDirty = true;
        }

        /// <summary>Re-reads the ash heights everywhere (e.g. after a save restored the mountain).</summary>
        public void MarkTerrainDirty()
        {
            if (_seenVersions == null)
            {
                return;
            }

            for (int i = 0; i < _seenVersions.Length; i++)
            {
                _seenVersions[i] = -1;
            }

            _terrainDirty = true;
        }

        /// <summary>Re-reads the ash heights of every chunk column that changed since the last call.</summary>
        public void RefreshTerrain()
        {
            if (_world == null)
            {
                return;
            }

            bool all = _terrainDirty;
            _terrainDirty = false;
            for (int cz = 0; cz < _world.ChunksZ; cz++)
            {
                for (int cx = 0; cx < _world.ChunksX; cx++)
                {
                    bool changed = all;
                    for (int cy = 0; cy < _world.ChunksY; cy++)
                    {
                        int index = _world.ChunkIndex(cx, cy, cz);
                        int version = _world.VersionOf(index);
                        if (version != _seenVersions[index])
                        {
                            _seenVersions[index] = version;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        RefreshChunkColumn(cx, cz);
                    }
                }
            }
        }

        /// <summary>Marks a cell as blocked until a time (something the walker bumped into that the grid doesn't know).</summary>
        public void BlockUntil(int x, int z, float until)
        {
            if (InBounds(x, z))
            {
                _blockedUntil[x + z * Width] = until;
            }
        }

        public bool IsWalkable(int x, int z)
        {
            if (!InBounds(x, z))
            {
                return false;
            }

            int index = x + z * Width;
            return !_blocked[index] && _blockedUntil[index] <= Now;
        }

        public bool CanStep(int fromX, int fromZ, int toX, int toZ)
        {
            if (!IsWalkable(toX, toZ) || !InBounds(fromX, fromZ))
            {
                return false;
            }

            float distance = fromX != toX && fromZ != toZ ? 1.41421356f : 1f;
            float walkable = _maxStep * distance;
            float rise = HeightAt(toX, toZ) - HeightAt(fromX, fromZ);
            if (rise <= walkable)
            {
                return rise >= -Mathf.Max(walkable, _maxDrop);
            }

            return IsJump(fromX, fromZ, toX, toZ, rise, walkable);
        }

        /// <summary>Whether going from one cell to the next means jumping up (too high a rise to walk).</summary>
        public bool NeedsJump(int fromX, int fromZ, int toX, int toZ)
        {
            float distance = fromX != toX && fromZ != toZ ? 1.41421356f : 1f;
            return HeightAt(toX, toZ) - HeightAt(fromX, fromZ) > _maxStep * distance;
        }

        // A jump up onto a ledge: low enough, and the ground beyond it goes on level or down. A slope that keeps rising
        // too steeply isn't a ledge: hopping up it would only slide her back.
        private bool IsJump(int fromX, int fromZ, int toX, int toZ, float rise, float walkable)
        {
            if (rise > _maxJump)
            {
                return false;
            }

            int beyondX = toX + (toX - fromX);
            int beyondZ = toZ + (toZ - fromZ);
            return !InBounds(beyondX, beyondZ) || HeightAt(beyondX, beyondZ) - HeightAt(toX, toZ) <= walkable;
        }

        public float HeightAt(int x, int z)
        {
            return InBounds(x, z) ? _height[x + z * Width] : 0f;
        }

        public bool InBounds(int x, int z)
        {
            return x >= 0 && z >= 0 && x < Width && z < Depth;
        }

        public bool TryCellOf(Vector3 world, out Vector2Int cell)
        {
            cell = new Vector2Int(Mathf.FloorToInt((world.x - Min.x) / CellSize), Mathf.FloorToInt((world.z - Min.y) / CellSize));
            return InBounds(cell.x, cell.y);
        }

        /// <summary>The cell's center at its standing height (world).</summary>
        public Vector3 CenterOf(int x, int z)
        {
            return new Vector3(Min.x + (x + 0.5f) * CellSize, HeightAt(x, z), Min.y + (z + 0.5f) * CellSize);
        }

        private void RefreshChunkColumn(int cx, int cz)
        {
            float chunkSize = _world.ChunkCells * _world.CellSize;
            float x0 = _terrainOffset.x + _world.Origin.x + cx * chunkSize;
            float z0 = _terrainOffset.z + _world.Origin.z + cz * chunkSize;
            int fromX = Mathf.Max(0, Mathf.FloorToInt((x0 - Min.x) / CellSize));
            int fromZ = Mathf.Max(0, Mathf.FloorToInt((z0 - Min.y) / CellSize));
            int toX = Mathf.Min(Width - 1, Mathf.CeilToInt((x0 + chunkSize - Min.x) / CellSize));
            int toZ = Mathf.Min(Depth - 1, Mathf.CeilToInt((z0 + chunkSize - Min.y) / CellSize));
            for (int z = fromZ; z <= toZ; z++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    RefreshCell(x, z);
                }
            }
        }

        private void RefreshCell(int x, int z)
        {
            int index = x + z * Width;
            float ground = _ground[index];
            float worldX = Min.x + (x + 0.5f) * CellSize;
            float worldZ = Min.y + (z + 0.5f) * CellSize;
            float height = ground;
            if (_world.TryFindTop(worldX - _terrainOffset.x, worldZ - _terrainOffset.z, out float top))
            {
                float ashTop = top + _terrainOffset.y;
                if (ashTop > ground + _minAshThickness)
                {
                    height = ashTop;
                }
            }

            _height[index] = height;
        }
    }
}
