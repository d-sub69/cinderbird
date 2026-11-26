using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// The diggable mountain as data: a <see cref="VoxelWorld"/> plus the embers buried in each chunk.
    /// Digging carves the world and extracts embers from every chunk the dig touches. Keeps running totals
    /// (ash volume, buried embers) so the HUD never has to walk millions of samples.
    /// </summary>
    public sealed class AshTerrain
    {
        // Multiplier keeping each chunk's ember seed distinct but reproducible.
        private const int ChunkSeedStride = 7919;
        // A grazed ember is laid this far inside the hole's edge, so it ends up in the open.
        private const float ExposeInset = 0.05f;

        private readonly EmberField[] _embers;
        // Chunks whose embers changed since generation; a dig can take embers without changing any sample.
        private readonly bool[] _embersChanged;
        private readonly int[] _emberVersions;
        private readonly LayerEmberCounts _counts;
        private readonly LooseEmberList _loose = new LooseEmberList();
        private readonly List<Ember> _digScratch = new List<Ember>();
        // Generated surface height per sample column, in samples (x + z * SamplesX).
        private float[] _naturalSurface;
        private readonly List<int> _chunkScratch = new List<int>();
        private readonly List<BuriedFind> _finds = new List<BuriedFind>();

        public AshTerrain(VoxelWorld world, int layerCount = 1)
        {
            World = world;
            Locks = new LayerLocks(layerCount);
            _counts = new LayerEmberCounts(Locks.Count);
            _embersChanged = new bool[world.ChunkCount];
            _emberVersions = new int[world.ChunkCount];
            _embers = new EmberField[world.ChunkCount];
            for (int i = 0; i < _embers.Length; i++)
            {
                _embers[i] = new EmberField();
            }
        }

        public VoxelWorld World { get; }

        /// <summary>Which ash layers can be dug; only the outermost starts open.</summary>
        public LayerLocks Locks { get; }

        /// <summary>Cubic meters of ash left; updated on every dig.</summary>
        public float TotalAshVolume { get; private set; }

        /// <summary>Embers still on the mountain: buried, or lying loose until collected.</summary>
        public int BuriedEmberCount => _counts.Total;

        public EmberField EmbersIn(int chunkIndex)
        {
            return _embers[chunkIndex];
        }

        /// <param name="layout">Assigns ash layers by depth; null leaves everything in layer 0.</param>
        /// <summary>Where the layers were laid out at generation; null when everything is layer 0.</summary>
        public LayerLayout Layout { get; private set; }

        public void Generate(AshPileShape shape, LayerLayout layout = null)
        {
            AshPileGenerator.Generate(World, shape, layout);
            Layout = layout;
            StoreNaturalSurface(shape);
            TotalAshVolume = World.TotalAshVolume();
            World.MarkAllDirty();
            ClearModified();
        }

        /// <summary>
        /// Height of the freshly generated surface over a sample column, in samples (the same units as
        /// <see cref="VoxelWorld"/> sample indices); NaN before <see cref="Generate"/>. Settling uses it so the
        /// mountain's own slopes count as stable even where they're steeper than loose ash would rest.
        /// </summary>
        /// <remarks>Read from a table filled at generation, so settling can call it from a background thread.</remarks>
        public float NaturalSurfaceSample(int x, int z)
        {
            return _naturalSurface != null ? _naturalSurface[x + z * World.SamplesX] : float.NaN;
        }

        private void StoreNaturalSurface(AshPileShape shape)
        {
            _naturalSurface ??= new float[World.SamplesX * World.SamplesZ];
            for (int z = 0; z < World.SamplesZ; z++)
            {
                for (int x = 0; x < World.SamplesX; x++)
                {
                    Vector3 column = World.SamplePosition(x, 0, z);
                    float height = AshPileGenerator.SurfaceHeightAt(column.x, column.z, shape);
                    _naturalSurface[x + z * World.SamplesX] = (height - World.Origin.y) / World.CellSize;
                }
            }
        }

        /// <summary>Moves a buried ember to a new spot (e.g. carried by sliding ash), across chunks if needed.</summary>
        /// <param name="fromChunk">The chunk whose list holds the ember now.</param>
        /// <returns>The chunk now holding it, or -1 if it wasn't there (e.g. it was dug out meanwhile).</returns>
        public int MoveEmber(int fromChunk, Ember from, Ember to)
        {
            if (!_embers[fromChunk].Remove(from))
            {
                return -1;
            }

            int toChunk = World.ChunkIndexAt(to.Position);
            _embers[toChunk].Add(to);
            _embersChanged[fromChunk] = true;
            _embersChanged[toChunk] = true;
            _emberVersions[fromChunk]++;
            _emberVersions[toChunk]++;
            return toChunk;
        }

        /// <summary>Whether a chunk's ash or embers changed since generation, i.e. whether a save needs it.</summary>
        public bool IsChunkModified(int chunkIndex)
        {
            return World.IsModified(chunkIndex) || _embersChanged[chunkIndex];
        }

        /// <summary>Replaces a chunk's buried embers with saved ones. Call <see cref="FinishRestore"/> afterwards.</summary>
        public void ReplaceEmbers(int chunkIndex, IReadOnlyList<Ember> embers)
        {
            _embers[chunkIndex].ReplaceWith(embers);
            _emberVersions[chunkIndex]++;
        }

        /// <summary>
        /// Goes up whenever the chunk's ash or embers change (both counters only ever grow, so their sum does too).
        /// A save compares readings to copy only the chunks that changed since it last looked.
        /// </summary>
        public int ChangeVersionOf(int chunkIndex)
        {
            return World.VersionOf(chunkIndex) + _emberVersions[chunkIndex];
        }

        /// <summary>
        /// After restoring saved chunks: only those count as modified (the restore itself touches their
        /// neighbors' shared borders), and the running totals are recounted.
        /// </summary>
        public void FinishRestore(IReadOnlyList<int> restoredChunks)
        {
            ClearModified();
            for (int i = 0; i < restoredChunks.Count; i++)
            {
                World.MarkModified(restoredChunks[i]);
                _embersChanged[restoredChunks[i]] = true;
            }

            TotalAshVolume = World.TotalAshVolume();
            RecountBuried();
        }

        private void ClearModified()
        {
            World.ClearModified();
            Array.Clear(_embersChanged, 0, _embersChanged.Length);
        }

        /// <summary>Scatters embers chunk by chunk, evenly through each ash layer at its own rate and with its tiers.</summary>
        public void SpawnLayeredEmbers(LayerSpawnTable table, int seed, float minDensity)
        {
            // Chunks in parallel: each has its own seed and its own ember list and only reads the ash, so the result is
            // the same as one after another, just sooner (tens of thousands of embers at start-up).
            List<Ember>[] spawned = new List<Ember>[_embers.Length];
            System.Threading.Tasks.Parallel.For(0, _embers.Length, i =>
            {
                spawned[i] = new List<Ember>();
                EmberSpawner.SpawnLayered(World.GetChunk(i), table, seed * ChunkSeedStride + i, minDensity, spawned[i]);
            });

            for (int i = 0; i < _embers.Length; i++)
            {
                _embers[i].AddRange(spawned[i]);
            }

            RecountBuried();
            _counts.SetInitialToCurrent();
        }

        /// <summary>Buries one specific ember (e.g. the single Heart Ember) where it says, as part of the mountain.</summary>
        public void PlaceEmber(Ember ember)
        {
            _embers[World.ChunkIndexAt(ember.Position)].Add(ember);
            _counts.AddGenerated(ember);
        }

        /// <summary>Share of a layer's embers already taken out of the pile (0..1); 1 for a layer that had none.</summary>
        public float ClearedShare(int layer)
        {
            return _counts.ClearedShare(layer);
        }

        public int InitialEmbersIn(int layer)
        {
            return _counts.InitialIn(layer);
        }

        /// <summary>Embers of a layer still on the mountain (buried or loose).</summary>
        public int BuriedEmbersIn(int layer)
        {
            return _counts.OnMountainIn(layer);
        }

        private void RecountBuried()
        {
            _counts.ClearOnMountain();
            for (int i = 0; i < _embers.Length; i++)
            {
                EmberField field = _embers[i];
                for (int e = 0; e < field.Count; e++)
                {
                    _counts.Returned(field[e]);
                }
            }

            // Loose embers are still on the mountain: they count until collected.
            for (int i = 0; i < _loose.Count; i++)
            {
                _counts.Returned(_loose[i].Ember);
            }
        }

        /// <summary>Scatters embers chunk by chunk; same parameters always give the same mountain.</summary>
        public void SpawnEmbers(EmberSpawnParameters parameters, IReadOnlyList<float> tierWeights)
        {
            List<Ember> spawned = new List<Ember>();
            for (int i = 0; i < _embers.Length; i++)
            {
                spawned.Clear();
                EmberSpawnParameters chunkParameters = new EmberSpawnParameters(
                    parameters.EmbersPerCubicMeter, parameters.MinDensity, parameters.Seed * ChunkSeedStride + i);
                EmberSpawner.Spawn(World.GetChunk(i), chunkParameters, tierWeights, spawned);
                _embers[i].AddRange(spawned);
            }

            RecountBuried();
            _counts.SetInitialToCurrent();
        }

        /// <param name="center">World-local position (the terrain object's local space).</param>
        /// <param name="extractedEmbers">Embers inside the sphere are appended here.</param>
        /// <param name="removedByLayer">If given, cubic meters taken from each layer are added to it.</param>
        /// <param name="exposedEmbers">If given, embers the dig only grazed are moved here (see <see cref="StartDig"/>).</param>
        /// <param name="exposeMargin">How far beyond the dug sphere an ember counts as grazed (meters).</param>
        public DigResult Dig(Vector3 center, float radius, List<Ember> extractedEmbers, float[] removedByLayer = null,
            List<LooseEmber> exposedEmbers = null, float exposeMargin = 0f, List<BuriedFind> uncoveredFinds = null)
        {
            DigResult result = StartDig(center, radius, extractedEmbers, removedByLayer, exposedEmbers, exposeMargin, uncoveredFinds);
            CarveDig(center, radius);
            return result;
        }

        /// <summary>
        /// First half of a dig that is carved over a moment: takes the embers and counts the ash exactly as
        /// <see cref="Dig"/> would, but carves nothing. Follow with <see cref="CarveDig"/> calls of growing radius,
        /// ending with the full one, and change nothing else in between.
        /// </summary>
        /// <param name="extractedEmbers">Embers inside the sphere (buried or loose) are appended: they go to the sieve.</param>
        /// <param name="exposedEmbers">
        /// Buried embers just beyond the sphere (within <paramref name="exposeMargin"/>): the dig reached their top or
        /// middle without taking them. They leave the ash, placed just inside the hole's edge, and are appended here
        /// for the caller to add as loose embers once the hole is open (<see cref="AddLoose(Ember, Vector3)"/>), each
        /// with where it lay (<see cref="LooseEmber.CameFrom"/>; its id is not set yet). Null leaves them buried.
        /// </param>
        /// <param name="uncoveredFinds">
        /// If given, buried finds (offerings) the dig reaches (within its radius plus <paramref name="exposeMargin"/>) are moved
        /// here: they are uncovered, not taken, and lie where they were for the player to pick up.
        /// </param>
        public DigResult StartDig(Vector3 center, float radius, List<Ember> extractedEmbers, float[] removedByLayer,
            List<LooseEmber> exposedEmbers, float exposeMargin, List<BuriedFind> uncoveredFinds = null)
        {
            DigResult measured = SphereDigBrush.Measure(World, center, radius, Locks, removedByLayer);
            float reach = exposedEmbers != null ? radius + Mathf.Max(0f, exposeMargin) : radius;
            Vector3 extent = Vector3.one * reach;
            World.ChunksOverlapping(center - extent, center + extent, _chunkScratch);
            int emberCount = 0;
            for (int i = 0; i < _chunkScratch.Count; i++)
            {
                int chunk = _chunkScratch[i];
                _digScratch.Clear();
                if (_embers[chunk].ExtractWithin(center, reach, _digScratch, Locks) == 0)
                {
                    continue;
                }

                _embersChanged[chunk] = true;
                _emberVersions[chunk]++;
                foreach (Ember ember in _digScratch)
                {
                    Vector3 offset = ember.Position - center;
                    float distance = offset.magnitude;
                    if (distance <= radius || exposedEmbers == null)
                    {
                        extractedEmbers.Add(ember);
                        _counts.Taken(ember);
                        emberCount++;
                    }
                    else if (TryExposedSpot(center, radius, ember.Position, out Vector3 spot))
                    {
                        // Still in the pile as far as clearing goes: only collecting it counts.
                        exposedEmbers.Add(new LooseEmber(-1, new Ember(spot, ember.TierIndex, ember.Layer), ember.Position));
                    }
                    else
                    {
                        // No open space at its height: it stays where it is, under a thin cover.
                        _embers[chunk].Add(ember);
                    }
                }
            }

            emberCount += TakeLooseWithin(center, radius, extractedEmbers);
            if (uncoveredFinds != null)
            {
                UncoverFindsWithin(center, radius + Mathf.Max(0f, exposeMargin), uncoveredFinds);
            }

            TotalAshVolume -= measured.RemovedVolume;
            return new DigResult(measured.RemovedVolume, emberCount, measured.BlockedLayer);
        }

        /// <summary>
        /// Where an ember the dig grazed comes out: straight sideways into the hole at its own height, never up or
        /// toward the dig, so gravity takes it from there. An ember below the hole's floor or above its top has no
        /// open space at its height and isn't uncovered.
        /// </summary>
        public static bool TryExposedSpot(Vector3 digCenter, float radius, Vector3 ember, out Vector3 spot)
        {
            spot = ember;
            float height = ember.y - digCenter.y;
            float holeRadiusSquared = radius * radius - height * height;
            if (holeRadiusSquared <= ExposeInset * ExposeInset * 4f)
            {
                return false;
            }

            Vector2 sideways = new Vector2(ember.x - digCenter.x, ember.z - digCenter.z);
            float distance = sideways.magnitude;
            float holeRadius = Mathf.Sqrt(holeRadiusSquared) - ExposeInset;
            if (distance <= holeRadius)
            {
                return true;
            }

            Vector2 edge = sideways * (holeRadius / distance);
            spot = new Vector3(digCenter.x + edge.x, ember.y, digCenter.z + edge.y);
            return true;
        }

        /// <summary>Buried finds (offerings) still in the ash.</summary>
        public IReadOnlyList<BuriedFind> Finds => _finds;

        /// <summary>Buries a find (an offering) at a spot; it takes the ash layer there.</summary>
        public void PlaceFind(int id, Vector3 position)
        {
            Vector3 sample = (position - World.Origin) / World.CellSize;
            int x = Mathf.Clamp(Mathf.RoundToInt(sample.x), 0, World.SamplesX - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(sample.y), 0, World.SamplesY - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(sample.z), 0, World.SamplesZ - 1);
            RemoveFind(id);
            _finds.Add(new BuriedFind(id, position, World.GetLayer(x, y, z)));
        }

        /// <summary>Takes a find out of the ash for good (e.g. a save says it was found already).</summary>
        public void RemoveFind(int id)
        {
            for (int i = _finds.Count - 1; i >= 0; i--)
            {
                if (_finds[i].Id == id)
                {
                    _finds.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Height of the freshly generated surface over a terrain-local spot (x and z used), in local units, for placing
        /// things at a depth below it; NaN before <see cref="Generate"/>.
        /// </summary>
        public float NaturalSurfaceHeightAt(Vector3 position)
        {
            Vector3 sample = (position - World.Origin) / World.CellSize;
            int x = Mathf.Clamp(Mathf.RoundToInt(sample.x), 0, World.SamplesX - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(sample.z), 0, World.SamplesZ - 1);
            return World.Origin.y + NaturalSurfaceSample(x, z) * World.CellSize;
        }

        /// <summary>
        /// Blows the top of the ash off a round area: every column within <paramref name="radius"/> of the center loses up to
        /// <paramref name="depth"/> meters off its surface, the full depth in the middle and fading out at the rim. The ash
        /// is gone (not collected). Sealed layers aren't touched. Embers in the blown-off skin are moved to
        /// <paramref name="exposedEmbers"/>, set on the new surface, for the caller to lay loose once the meshes are up
        /// (<see cref="AddLoose(Ember, Vector3)"/>); finds there are moved to <paramref name="uncoveredFinds"/>.
        /// Change nothing else in between, as with <see cref="StartDig"/>.
        /// </summary>
        /// <param name="center">Middle of the area (local); only x and z matter for the columns.</param>
        public DigResult BlowSurface(Vector3 center, float radius, float depth, List<LooseEmber> exposedEmbers, List<BuriedFind> uncoveredFinds)
        {
            float removed = 0f;
            if (radius <= 0f || depth <= 0f)
            {
                return default;
            }

            float cell = World.CellSize;
            int x0 = Mathf.Max(1, Mathf.FloorToInt((center.x - radius - World.Origin.x) / cell));
            int x1 = Mathf.Min(World.SamplesX - 2, Mathf.CeilToInt((center.x + radius - World.Origin.x) / cell));
            int z0 = Mathf.Max(1, Mathf.FloorToInt((center.z - radius - World.Origin.z) / cell));
            int z1 = Mathf.Min(World.SamplesZ - 2, Mathf.CeilToInt((center.z + radius - World.Origin.z) / cell));
            int blockedLayer = -1;
            for (int iz = z0; iz <= z1; iz++)
            {
                for (int ix = x0; ix <= x1; ix++)
                {
                    Vector3 column = World.SamplePosition(ix, 0, iz);
                    float distance = Mathf.Sqrt((column.x - center.x) * (column.x - center.x) + (column.z - center.z) * (column.z - center.z));
                    float strip = depth * BlowFalloff(distance / radius);
                    if (strip > 0f)
                    {
                        removed += BlowColumn(ix, iz, strip, ref blockedLayer);
                    }
                }
            }

            ExposeBlownEmbers(center, radius, depth, exposedEmbers);
            UncoverBlownFinds(center, radius, uncoveredFinds);
            TotalAshVolume -= removed;
            return new DigResult(removed, 0, removed > 0f ? -1 : blockedLayer);
        }

        // Full depth over the inner 70 % of the radius, easing to nothing at the rim, so the edge is a soft slope, not a step.
        private static float BlowFalloff(float t)
        {
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));
        }

        // Lowers one sample column's surface by `strip` meters: a CSG "no ash above this height", like a dig but flat.
        private float BlowColumn(int ix, int iz, float strip, ref int blockedLayer)
        {
            if (!World.TryFindTop(World.SamplePosition(ix, 0, iz).x, World.SamplePosition(ix, 0, iz).z, out float top))
            {
                return 0f;
            }

            float cell = World.CellSize;
            float newTop = top - strip;
            float removedFill = 0f;
            int from = Mathf.Min(World.SamplesY - 2, Mathf.CeilToInt((top - World.Origin.y) / cell) + 1);
            for (int iy = from; iy >= 1; iy--)
            {
                float y = World.Origin.y + iy * cell;
                if (y < newTop - cell)
                {
                    break;
                }

                float carved = Mathf.Clamp((newTop - y) / cell, VoxelGrid.Air, VoxelGrid.Ash);
                float current = World.GetDensity(ix, iy, iz);
                if (carved >= current)
                {
                    continue;
                }

                int layer = World.GetLayer(ix, iy, iz);
                if (!Locks.IsUnlocked(layer))
                {
                    blockedLayer = blockedLayer < 0 ? layer : blockedLayer;
                    continue;
                }

                removedFill += VoxelGrid.Fill(current) - VoxelGrid.Fill(carved);
                World.SetDensity(ix, iy, iz, carved);
            }

            return removedFill * World.CellVolume;
        }

        // Embers the wind laid bare: within the area and now at (or above) the new surface. They are set on the surface;
        // the rest go back where they were.
        private void ExposeBlownEmbers(Vector3 center, float radius, float depth, List<LooseEmber> exposed)
        {
            float reach = radius + depth + World.CellSize * 4f;
            Vector3 extent = new Vector3(reach, World.Size.y, reach);
            World.ChunksOverlapping(center - extent, center + extent, _chunkScratch);
            for (int i = 0; i < _chunkScratch.Count; i++)
            {
                int chunk = _chunkScratch[i];
                _digScratch.Clear();
                if (_embers[chunk].ExtractWithin(center, Mathf.Sqrt(reach * reach + World.Size.y * World.Size.y), _digScratch, Locks) == 0)
                {
                    continue;
                }

                bool changed = false;
                foreach (Ember ember in _digScratch)
                {
                    Vector3 offset = ember.Position - center;
                    offset.y = 0f;
                    if (offset.magnitude <= radius && World.TryFindTop(ember.Position.x, ember.Position.z, out float top) &&
                        ember.Position.y >= top - ExposeInset)
                    {
                        Vector3 spot = new Vector3(ember.Position.x, top, ember.Position.z);
                        exposed.Add(new LooseEmber(-1, new Ember(spot, ember.TierIndex, ember.Layer), ember.Position));
                        changed = true;
                    }
                    else
                    {
                        _embers[chunk].Add(ember);
                    }
                }

                if (changed)
                {
                    _embersChanged[chunk] = true;
                    _emberVersions[chunk]++;
                }
            }
        }

        private void UncoverBlownFinds(Vector3 center, float radius, List<BuriedFind> output)
        {
            for (int i = _finds.Count - 1; i >= 0; i--)
            {
                BuriedFind find = _finds[i];
                Vector3 offset = find.Position - center;
                offset.y = 0f;
                if (offset.magnitude <= radius && Locks.IsUnlocked(find.Layer) &&
                    World.TryFindTop(find.Position.x, find.Position.z, out float top) && find.Position.y >= top - ExposeInset * 4f)
                {
                    output.Add(find);
                    _finds.RemoveAt(i);
                }
            }
        }

        private void UncoverFindsWithin(Vector3 center, float radius, List<BuriedFind> output)
        {
            float radiusSquared = radius * radius;
            for (int i = _finds.Count - 1; i >= 0; i--)
            {
                BuriedFind find = _finds[i];
                if ((find.Position - center).sqrMagnitude <= radiusSquared && Locks.IsUnlocked(find.Layer))
                {
                    output.Add(find);
                    _finds.RemoveAt(i);
                }
            }
        }

        /// <summary>Embers lying loose on a surface (not buried): uncovered by digs, or dropped. Order may change.</summary>
        public IReadOnlyList<LooseEmber> LooseEmbers => _loose.Items;

        /// <summary>
        /// Lays an ember loose at its position; it still counts as in the pile until collected. Once loose it is never
        /// buried again (<see cref="LooseEmber.Rest"/>).
        /// </summary>
        /// <returns>Its id, stable while it stays loose.</returns>
        public int AddLoose(Ember ember)
        {
            return AddLoose(ember, ember.Position);
        }

        /// <param name="cameFrom">Where it was before lying loose (e.g. its spot in the ash), for views to animate.</param>
        public int AddLoose(Ember ember, Vector3 cameFrom)
        {
            return _loose.Add(ember, cameFrom);
        }

        /// <summary>A brand-new loose ember (e.g. one that slipped through the sieve): counted as in the pile again.</summary>
        public int AddDroppedLoose(Ember ember)
        {
            _counts.Returned(ember);
            return AddLoose(ember);
        }

        /// <returns>The index in <see cref="LooseEmbers"/> of the loose ember with this id, or -1 if it isn't loose (any more).</returns>
        public int IndexOfLoose(int id)
        {
            return _loose.IndexOf(id);
        }

        public void MoveLoose(int index, Vector3 position)
        {
            _loose.MoveTo(index, position);
        }

        /// <summary>Sets how high a loose ember sits above the ash (<see cref="LooseEmber.Rest"/>); NaN = not known.</summary>
        public void SetLooseRest(int index, float rest)
        {
            _loose.SetRest(index, rest);
        }

        /// <summary>Collects a loose ember (e.g. into the sieve): it no longer counts as in the pile.</summary>
        /// <summary>
        /// The crust melts when a deeper layer opens: every bit of ash of the layers above <paramref name="layer"/> is gone,
        /// their buried and loose embers come out (counted as taken), and offerings that lay in them drop to the new surface.
        /// </summary>
        /// <param name="released">Receives the embers that came out (appended).</param>
        /// <param name="uncovered">Receives the offerings uncovered, at the spot they dropped to (appended).</param>
        public void MeltLayersAbove(int layer, List<Ember> released, List<BuriedFind> uncovered)
        {
            for (int z = 1; z < World.SamplesZ - 1; z++)
            {
                for (int y = 1; y < World.SamplesY - 1; y++)
                {
                    for (int x = 1; x < World.SamplesX - 1; x++)
                    {
                        if (World.GetDensity(x, y, z) > VoxelGrid.Air && World.GetLayer(x, y, z) < layer)
                        {
                            World.SetDensity(x, y, z, VoxelGrid.Air);
                        }
                    }
                }
            }

            for (int chunk = 0; chunk < _embers.Length; chunk++)
            {
                int first = released.Count;
                if (_embers[chunk].ExtractLayersAbove(layer, released) == 0)
                {
                    continue;
                }

                for (int i = first; i < released.Count; i++)
                {
                    _counts.Taken(released[i]);
                }

                _embersChanged[chunk] = true;
                _emberVersions[chunk]++;
            }

            for (int i = _loose.Count - 1; i >= 0; i--)
            {
                if (_loose[i].Ember.Layer < layer)
                {
                    released.Add(TakeLoose(i));
                }
            }

            for (int i = _finds.Count - 1; i >= 0; i--)
            {
                BuriedFind find = _finds[i];
                if (find.Layer >= layer)
                {
                    continue;
                }

                _finds.RemoveAt(i);
                uncovered.Add(new BuriedFind(find.Id, DropToSurface(find.Position), find.Layer));
            }

            TotalAshVolume = World.TotalAshVolume();
        }

        // Straight down from a spot to the first ash (or the ground), so a find doesn't hang in the air.
        private Vector3 DropToSurface(Vector3 position)
        {
            float step = World.CellSize * 0.5f;
            float floor = World.Origin.y + World.CellSize;
            Vector3 point = position;
            while (point.y > floor && World.SampleDensity(point) <= 0f)
            {
                point.y -= step;
            }

            point.y = Mathf.Max(point.y + step, floor);
            return point;
        }

        public Ember TakeLoose(int index)
        {
            Ember ember = _loose[index].Ember;
            _loose.RemoveAt(index);
            _counts.Taken(ember);
            return ember;
        }

        /// <summary>Puts back the loose embers from a save; call before <see cref="FinishRestore"/> or recounts itself.</summary>
        public void ReplaceLoose(IReadOnlyList<Ember> embers)
        {
            _loose.Clear();
            for (int i = 0; i < embers.Count; i++)
            {
                AddLoose(embers[i]);
            }

            RecountBuried();
        }

        // Loose embers inside a dig go to the sieve with the ash.
        private int TakeLooseWithin(Vector3 center, float radius, List<Ember> extractedEmbers)
        {
            int taken = 0;
            float radiusSquared = radius * radius;
            for (int i = _loose.Count - 1; i >= 0; i--)
            {
                if ((_loose[i].Ember.Position - center).sqrMagnitude <= radiusSquared)
                {
                    extractedEmbers.Add(TakeLoose(i));
                    taken++;
                }
            }

            return taken;
        }

        /// <summary>
        /// Carves (part of) a dig begun with <see cref="StartDig"/>. Carving is a CSG subtract, so growing radii
        /// end exactly where one carve of the full radius would; the ash was already counted at the start.
        /// </summary>
        public void CarveDig(Vector3 center, float radius)
        {
            SphereDigBrush.Dig(World, center, radius, Locks, null);
        }

        public void CopyAllEmbers(List<Ember> output)
        {
            output.Clear();
            for (int i = 0; i < _embers.Length; i++)
            {
                EmberField field = _embers[i];
                for (int e = 0; e < field.Count; e++)
                {
                    output.Add(field[e]);
                }
            }
        }
    }
}
