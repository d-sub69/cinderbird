using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Lets loose ash around a dig settle like real ash: overhangs collapse and slopes steeper than the angle of
    /// repose slide down until they rest. Works on the area around one dig as columns of ash, each a stack of
    /// layer runs on top of whatever can't move (the floor, or locked layers, which behave like rock):
    /// <list type="bullet">
    /// <item>Loading a column stacks its loose ash without gaps, so caves and overhangs collapse.</item>
    /// <item>Each step, a column standing higher than a neighbor by more than the slope allows sheds ash onto it.
    /// A slope may also stay as steep as the freshly generated mountain was there, so only what digging made
    /// steeper slides, never the mountain itself.</item>
    /// <item>Writing a column back rebuilds its densities from its height with the generator's one-cell ramp, so
    /// the amount of ash is exactly what the column holds: settling never creates or destroys ash.</item>
    /// </list>
    /// Embers in sliding ash ride along and stay buried. Plain C# (no Unity objects) so it can be unit tested.
    /// </summary>
    public sealed class AshSettler
    {
        // Height differences within this many cells count as settled; keeps rounding from moving ash forever.
        private const float StableEpsilon = 0.02f;
        private const float MinTransfer = 1e-4f;
        // Embers stay at least this many cells below the surface, so they never end up in the open.
        private const float EmberMinDepth = 0.75f;
        private const int MaxStepsToRest = 4000;

        // The 8 neighbors: 4 sides, then 4 diagonals.
        private static readonly int[] NeighborX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] NeighborZ = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private static readonly float[] NeighborDistance = { 1f, 1f, 1f, 1f, 1.4142135f, 1.4142135f, 1.4142135f, 1.4142135f };

        private readonly SettleParameters _parameters;
        private readonly List<LayerRun> _popped = new List<LayerRun>();
        private readonly List<SettlingEmber> _embers = new List<SettlingEmber>();
        private readonly List<int> _chunkScratch = new List<int>();
        private readonly float[] _excess = new float[8];

        private AshTerrain _terrain;
        private int _x0;
        private int _z0;
        private int _width;
        private int _depth;

        // Per column of the area (index = x + z * width).
        private float[] _mass;
        private int[] _base;
        private int[] _top;
        private float[] _natural;
        private bool[] _active;
        private bool[] _nextActive;
        private bool[] _changed;
        private List<LayerRun>[] _runs;
        private List<int>[] _columnEmbers;

        // One column of samples, read in one go (see VoxelWorld.CopyColumn).
        private float[] _columnDensities;
        private int _samplesY;
        // Per area column and sample: loose ash below that sample as loaded (row = column * SamplesY).
        private float[] _ashBelowSample;
        private byte[] _columnLayers;

        public AshSettler(SettleParameters parameters)
        {
            _parameters = parameters;
        }

        /// <summary>Whether an area is being settled (between <see cref="Begin"/> and <see cref="End"/>).</summary>
        public bool IsActive => _terrain != null;

        public SettleParameters Parameters => _parameters;

        /// <summary>Local-space box around what the last <see cref="Commit"/> wrote; empty if it wrote nothing.</summary>
        public Bounds LastCommitBounds { get; private set; }

        public bool LastCommitChangedAnything { get; private set; }

        /// <summary>
        /// Starts settling the area around a dig: loads every column within the dig radius plus the reach.
        /// Ends any area still settling first, at rest.
        /// </summary>
        /// <param name="center">Dig center in the terrain's local space.</param>
        public void Begin(AshTerrain terrain, Vector3 center, float digRadius)
        {
            if (IsActive)
            {
                Finish();
            }

            _terrain = terrain;
            VoxelWorld world = terrain.World;
            float cellSize = world.CellSize;
            Vector3 cell = (center - world.Origin) / cellSize;
            int reach = Mathf.CeilToInt((digRadius + _parameters.Reach) / cellSize);
            int cx = Mathf.RoundToInt(cell.x);
            int cz = Mathf.RoundToInt(cell.z);
            // The world's outer samples are always air; columns stay one sample inside.
            _x0 = Mathf.Clamp(cx - reach, 1, world.SamplesX - 2);
            _z0 = Mathf.Clamp(cz - reach, 1, world.SamplesZ - 2);
            int x1 = Mathf.Clamp(cx + reach, 1, world.SamplesX - 2);
            int z1 = Mathf.Clamp(cz + reach, 1, world.SamplesZ - 2);
            _width = x1 - _x0 + 1;
            _depth = z1 - _z0 + 1;
            EnsureCapacity(_width * _depth);
            _samplesY = world.SamplesY;
            if (_columnDensities == null || _columnDensities.Length != _samplesY)
            {
                _columnDensities = new float[_samplesY];
                _columnLayers = new byte[_samplesY];
            }

            int tableSize = _width * _depth * _samplesY;
            if (_ashBelowSample == null || _ashBelowSample.Length < tableSize)
            {
                _ashBelowSample = new float[tableSize];
            }

            // Columns the dig touched may hold overhangs or caves: they start active and get rewritten.
            float digCells = digRadius / cellSize + 1.5f;
            float digCellsSquared = digCells * digCells;
            Array.Clear(_active, 0, _width * _depth);
            Array.Clear(_nextActive, 0, _width * _depth);
            Array.Clear(_changed, 0, _width * _depth);
            for (int z = 0; z < _depth; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int i = x + z * _width;
                    LoadColumn(i, _x0 + x, _z0 + z);
                    float dx = _x0 + x - cell.x;
                    float dz = _z0 + z - cell.z;
                    if (dx * dx + dz * dz <= digCellsSquared)
                    {
                        _changed[i] = true;
                        Activate(_active, x, z);
                    }
                }
            }

            TrackEmbers();
        }

        /// <summary>Runs up to <paramref name="steps"/> settling steps in memory (nothing is written until <see cref="Commit"/>).</summary>
        /// <returns>True while ash is still moving; false once the area is at rest.</returns>
        public bool Step(int steps)
        {
            if (!IsActive)
            {
                return false;
            }

            for (int step = 0; step < steps; step++)
            {
                if (!StepOnce())
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Writes every column changed since the last commit into the terrain, and moves its embers.</summary>
        public void Commit()
        {
            LastCommitChangedAnything = false;
            if (!IsActive)
            {
                return;
            }

            MoveTrackedEmbers();

            VoxelWorld world = _terrain.World;
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue, maxY = 0;
            for (int i = 0; i < _width * _depth; i++)
            {
                if (!_changed[i])
                {
                    continue;
                }

                int gx = _x0 + i % _width;
                int gz = _z0 + i / _width;
                maxY = Math.Max(maxY, WriteColumn(i, gx, gz));
                _changed[i] = false;
                minX = Math.Min(minX, gx);
                maxX = Math.Max(maxX, gx);
                minZ = Math.Min(minZ, gz);
                maxZ = Math.Max(maxZ, gz);
                LastCommitChangedAnything = true;
            }

            if (LastCommitChangedAnything)
            {
                Vector3 min = world.SamplePosition(minX, 0, minZ);
                Vector3 max = world.SamplePosition(maxX, maxY, maxZ);
                Bounds bounds = new Bounds();
                bounds.SetMinMax(min, max);
                LastCommitBounds = bounds;
            }
        }

        /// <summary>Settles the area to rest at once, writes it, and ends.</summary>
        public void Finish()
        {
            Step(MaxStepsToRest);
            Commit();
            End();
        }

        /// <summary>Stops without writing anything not yet committed.</summary>
        public void End()
        {
            _terrain = null;
            _embers.Clear();
        }

        private void EnsureCapacity(int count)
        {
            if (_mass != null && _mass.Length >= count)
            {
                for (int i = 0; i < count; i++)
                {
                    _runs[i].Clear();
                    _columnEmbers[i].Clear();
                }

                return;
            }

            _mass = new float[count];
            _base = new int[count];
            _top = new int[count];
            _natural = new float[count];
            _active = new bool[count];
            _nextActive = new bool[count];
            _changed = new bool[count];
            _runs = new List<LayerRun>[count];
            _columnEmbers = new List<int>[count];
            for (int i = 0; i < count; i++)
            {
                _runs[i] = new List<LayerRun>();
                _columnEmbers[i] = new List<int>();
            }
        }

        // Stacks the column's loose ash (everything above its highest locked sample) without gaps.
        private void LoadColumn(int i, int gx, int gz)
        {
            VoxelWorld world = _terrain.World;
            world.CopyColumn(gx, gz, _columnDensities, _columnLayers);
            LayerLocks locks = _terrain.Locks;
            int highest = world.SamplesY - 2;

            int lockedTop = 0;
            for (int y = highest; y >= 1; y--)
            {
                if (VoxelGrid.Fill(_columnDensities[y]) > 0f && !locks.IsUnlocked(_columnLayers[y]))
                {
                    lockedTop = y;
                    break;
                }
            }

            int columnBase = lockedTop + 1;
            List<LayerRun> runs = _runs[i];
            float mass = 0f;
            int top = columnBase - 1;
            int row = i * _samplesY;
            for (int y = 0; y <= columnBase && y < _samplesY; y++)
            {
                _ashBelowSample[row + y] = 0f;
            }

            for (int y = columnBase; y <= highest; y++)
            {
                _ashBelowSample[row + y] = mass;
                float fill = VoxelGrid.Fill(_columnDensities[y]);
                if (fill <= 0f)
                {
                    continue;
                }

                AppendRun(runs, _columnLayers[y], fill);
                mass += fill;
                top = y;
            }

            _ashBelowSample[row + highest + 1] = mass;

            _base[i] = columnBase;
            _mass[i] = mass;
            _top[i] = top;
            _natural[i] = _terrain.NaturalSurfaceSample(gx, gz);
        }

        // The surface height (in samples) of a column holding this much loose ash; the inverse of the one-cell
        // ramp WriteColumn builds, so heights and amounts always agree.
        private float Height(int i)
        {
            float mass = _mass[i];
            return _base[i] + (mass <= 0.5f ? 2f * mass - 1f : mass - 0.5f);
        }

        private bool StepOnce()
        {
            float maxRise = _parameters.MaxRisePerCell;
            bool moved = false;
            int count = _width * _depth;
            Array.Clear(_nextActive, 0, count);

            for (int i = 0; i < count; i++)
            {
                if (!_active[i] || _mass[i] <= MinTransfer)
                {
                    continue;
                }

                int x = i % _width;
                int z = i / _width;
                float height = Height(i);
                float most = 0f;
                float total = 0f;
                for (int k = 0; k < 8; k++)
                {
                    _excess[k] = 0f;
                    int nx = x + NeighborX[k];
                    int nz = z + NeighborZ[k];
                    if (nx < 0 || nz < 0 || nx >= _width || nz >= _depth)
                    {
                        // Ash never leaves the area: the reach caps how far one dig's slide can go.
                        continue;
                    }

                    int j = nx + nz * _width;
                    float allowed = maxRise * NeighborDistance[k];
                    float natural = _natural[i] - _natural[j];
                    if (natural > allowed)
                    {
                        allowed = natural;
                    }

                    float excess = height - Height(j) - allowed;
                    if (excess > StableEpsilon)
                    {
                        _excess[k] = excess;
                        total += excess;
                        most = Mathf.Max(most, excess);
                    }
                }

                if (total <= 0f)
                {
                    continue;
                }

                // Shedding half the largest excess would level that pair exactly; the flow rate eases into it.
                float outflow = Mathf.Min(_mass[i], _parameters.FlowRate * most * 0.5f);
                if (outflow < MinTransfer)
                {
                    continue;
                }

                Shed(i, x, z, outflow, total);
                moved = true;
            }

            bool[] swap = _active;
            _active = _nextActive;
            _nextActive = swap;
            return moved;
        }

        // Moves the top `outflow` of column i onto its lower neighbors, in proportion to how much each is too low.
        private void Shed(int i, int x, int z, float outflow, float totalExcess)
        {
            float remaining = _mass[i] - outflow;
            PopTop(_runs[i], outflow);
            _mass[i] = remaining;
            MarkChanged(i, x, z);

            // Embers in the shed slab go with it, to the neighbor whose share covers their place in the slab.
            List<int> columnEmbers = _columnEmbers[i];
            for (int e = columnEmbers.Count - 1; e >= 0; e--)
            {
                int index = columnEmbers[e];
                SettlingEmber ember = _embers[index];
                if (ember.AshBelow <= remaining)
                {
                    continue;
                }

                // Each neighbor gets a thinner copy of the whole slab, so the ember keeps its relative height in
                // it; which neighbor is spread by that same place, so a slab's embers don't all go one way.
                float place = Mathf.Clamp01((ember.AshBelow - remaining) / outflow);
                int receiver = -1;
                float receiverShare = 0f;
                float cumulative = 0f;
                for (int k = 0; k < 8; k++)
                {
                    if (_excess[k] <= 0f)
                    {
                        continue;
                    }

                    receiver = k;
                    receiverShare = _excess[k] / totalExcess;
                    cumulative += receiverShare;
                    if (place <= cumulative)
                    {
                        break;
                    }
                }

                int j = x + NeighborX[receiver] + (z + NeighborZ[receiver]) * _width;
                _embers[index] = ember.MovedTo(j, _mass[j] + place * outflow * receiverShare);
                _columnEmbers[j].Add(index);
                columnEmbers.RemoveAt(e);
            }

            for (int k = 0; k < 8; k++)
            {
                if (_excess[k] <= 0f)
                {
                    continue;
                }

                int nx = x + NeighborX[k];
                int nz = z + NeighborZ[k];
                int j = nx + nz * _width;
                float share = _excess[k] / totalExcess;
                // Bottom of the slab first, so the receiving column gets the slab the right way up.
                for (int p = _popped.Count - 1; p >= 0; p--)
                {
                    AppendRun(_runs[j], _popped[p].Layer, _popped[p].Amount * share);
                }

                _mass[j] += outflow * share;
                MarkChanged(j, nx, nz);
            }
        }

        // Takes `amount` off the top of a column into _popped, topmost first.
        private void PopTop(List<LayerRun> runs, float amount)
        {
            _popped.Clear();
            float remaining = amount;
            while (remaining > 0f && runs.Count > 0)
            {
                int last = runs.Count - 1;
                LayerRun run = runs[last];
                float take = Mathf.Min(run.Amount, remaining);
                _popped.Add(new LayerRun(run.Layer, take));
                if (take >= run.Amount)
                {
                    runs.RemoveAt(last);
                }
                else
                {
                    runs[last] = run.WithAmount(run.Amount - take);
                }

                remaining -= take;
            }
        }

        private static void AppendRun(List<LayerRun> runs, byte layer, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            int last = runs.Count - 1;
            if (last >= 0 && runs[last].Layer == layer)
            {
                runs[last] = runs[last].WithAmount(runs[last].Amount + amount);
                return;
            }

            runs.Add(new LayerRun(layer, amount));
        }

        private void MarkChanged(int i, int x, int z)
        {
            _changed[i] = true;
            Activate(_nextActive, x, z);
        }

        // A column that changed may now be too high or too low for any neighbor, so all of them look again.
        private void Activate(bool[] flags, int x, int z)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    int nz = z + dz;
                    if (nx >= 0 && nz >= 0 && nx < _width && nz < _depth)
                    {
                        flags[nx + nz * _width] = true;
                    }
                }
            }
        }

        // Rebuilds the column's loose part from its height and its layer runs.
        /// <returns>The highest sample now holding ash.</returns>
        private int WriteColumn(int i, int gx, int gz)
        {
            VoxelWorld world = _terrain.World;
            // Compared against what's there, so only samples that really change are written (and re-meshed).
            world.CopyColumn(gx, gz, _columnDensities, _columnLayers);
            float height = Height(i);
            int columnBase = _base[i];
            int newTop = Mathf.Min(world.SamplesY - 2, Mathf.CeilToInt(height) + 1);
            int writeTop = Mathf.Max(newTop, _top[i]);
            List<LayerRun> runs = _runs[i];
            int run = 0;
            float runEnd = runs.Count > 0 ? runs[0].Amount : 0f;
            int highestAsh = columnBase - 1;

            for (int y = columnBase; y <= writeTop; y++)
            {
                float density = Mathf.Clamp(height - y, VoxelGrid.Air, VoxelGrid.Ash);
                if (Mathf.Abs(_columnDensities[y] - density) > 1e-6f)
                {
                    world.SetDensity(gx, y, gz, density);
                }

                if (density <= VoxelGrid.Air || runs.Count == 0)
                {
                    continue;
                }

                highestAsh = y;
                // The ash at this sample's center lies this deep into the column.
                float depthInColumn = y - columnBase + 0.5f;
                while (depthInColumn > runEnd && run < runs.Count - 1)
                {
                    run++;
                    runEnd += runs[run].Amount;
                }

                byte layer = runs[run].Layer;
                if (_columnLayers[y] != layer)
                {
                    world.SetLayer(gx, y, gz, layer);
                }
            }

            _top[i] = highestAsh;
            return writeTop;
        }

        // Finds every ember above the fixed part of an area column and records how much loose ash lies below it.
        private void TrackEmbers()
        {
            _embers.Clear();
            VoxelWorld world = _terrain.World;
            float cellSize = world.CellSize;
            Vector3 min = world.SamplePosition(_x0, 0, _z0) - Vector3.one * (cellSize * 0.5f);
            Vector3 max = world.SamplePosition(_x0 + _width - 1, world.SamplesY - 1, _z0 + _depth - 1) + Vector3.one * (cellSize * 0.5f);
            world.ChunksOverlapping(min, max, _chunkScratch);

            foreach (int chunk in _chunkScratch)
            {
                EmberField field = _terrain.EmbersIn(chunk);
                for (int e = 0; e < field.Count; e++)
                {
                    Ember ember = field[e];
                    Vector3 sample = (ember.Position - world.Origin) / cellSize;
                    int gx = Mathf.RoundToInt(sample.x);
                    int gz = Mathf.RoundToInt(sample.z);
                    int x = gx - _x0;
                    int z = gz - _z0;
                    if (x < 0 || z < 0 || x >= _width || z >= _depth)
                    {
                        continue;
                    }

                    int i = x + z * _width;
                    if (sample.y < _base[i] - 0.5f)
                    {
                        // In the fixed part (on or under locked ash): it stays where it is.
                        continue;
                    }

                    float below = Mathf.Clamp(AshBelow(i, sample.y), 0f, _mass[i]);
                    _columnEmbers[i].Add(_embers.Count);
                    // Packed ash puts the ember at base + below - 0.5; anything else means a gap (cave) below it.
                    bool gapBelow = Mathf.Abs(_base[i] + below - 0.5f - sample.y) > 0.05f;
                    _embers.Add(new SettlingEmber(ember, chunk, i, below, new Vector2(sample.x - gx, sample.z - gz), gapBelow));
                }
            }
        }

        // Loose ash below a height in a column as loaded: the samples below, plus the part of its own sample
        // under it. In packed ash that is exactly height - base + 0.5, so packed embers keep their exact place.
        private float AshBelow(int i, float sampleY)
        {
            int at = Mathf.Clamp(Mathf.RoundToInt(sampleY), _base[i], _samplesY - 2);
            int row = i * _samplesY;
            float below = _ashBelowSample[row + at];
            float fillAt = _ashBelowSample[row + at + 1] - below;
            float partOfOwn = Mathf.Clamp01(sampleY - at + 0.5f);
            return below + partOfOwn * fillAt;
        }

        // Re-places every tracked ember whose column changed, buried at its place in the column's ash.
        private void MoveTrackedEmbers()
        {
            VoxelWorld world = _terrain.World;
            for (int index = 0; index < _embers.Count; index++)
            {
                SettlingEmber tracked = _embers[index];
                if (tracked.Chunk < 0 || !_changed[tracked.Column])
                {
                    continue;
                }

                int i = tracked.Column;
                float height = Height(i);
                float currentY = (tracked.Ember.Position.y - world.Origin.y) / world.CellSize;
                // Untouched ash keeps its embers exactly where they are; only slid, unsupported or uncovered ones move.
                if (!tracked.NeedsPlacing && currentY <= height - EmberMinDepth)
                {
                    continue;
                }

                int gx = _x0 + i % _width;
                int gz = _z0 + i / _width;
                float sampleY = _base[i] + tracked.AshBelow - 0.5f;
                sampleY = Mathf.Max(_base[i] - 0.5f, Mathf.Min(sampleY, height - EmberMinDepth));

                Vector3 position = world.Origin + new Vector3(gx + tracked.Offset.x, sampleY, gz + tracked.Offset.y) * world.CellSize;
                Ember moved = new Ember(position, tracked.Ember.TierIndex, tracked.Ember.Layer);
                if (moved.Position.Equals(tracked.Ember.Position))
                {
                    continue;
                }

                int chunk = _terrain.MoveEmber(tracked.Chunk, tracked.Ember, moved);
                _embers[index] = chunk < 0 ? tracked.StoredAs(tracked.Ember, -1) : tracked.StoredAs(moved, chunk);
            }
        }
    }
}
