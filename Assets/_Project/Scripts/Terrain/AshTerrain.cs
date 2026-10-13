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

        private readonly EmberField[] _embers;
        // Chunks whose embers changed since generation; a dig can take embers without changing any sample.
        private readonly bool[] _embersChanged;
        private readonly int[] _emberVersions;
        // Embers per ash layer: as generated, and still in the pile. Their ratio is how cleared a layer is.
        private readonly int[] _initialByLayer;
        private readonly int[] _buriedByLayer;
        // Generated surface height per sample column, in samples (x + z * SamplesX).
        private float[] _naturalSurface;
        private readonly List<int> _chunkScratch = new List<int>();

        public AshTerrain(VoxelWorld world, int layerCount = 1)
        {
            World = world;
            Locks = new LayerLocks(layerCount);
            _initialByLayer = new int[Locks.Count];
            _buriedByLayer = new int[Locks.Count];
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

        public int BuriedEmberCount { get; private set; }

        public EmberField EmbersIn(int chunkIndex)
        {
            return _embers[chunkIndex];
        }

        /// <param name="layout">Assigns ash layers by depth; null leaves everything in layer 0.</param>
        public void Generate(AshPileShape shape, LayerLayout layout = null)
        {
            AshPileGenerator.Generate(World, shape, layout);
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

        /// <summary>Scatters embers chunk by chunk using each ash layer's own rate and tiers.</summary>
        public void SpawnLayeredEmbers(LayerSpawnTable table, int seed, float minDensity)
        {
            List<Ember> spawned = new List<Ember>();
            BuriedEmberCount = 0;
            for (int i = 0; i < _embers.Length; i++)
            {
                spawned.Clear();
                EmberSpawner.SpawnLayered(World.GetChunk(i), table, seed * ChunkSeedStride + i, minDensity, spawned);
                _embers[i].AddRange(spawned);
                BuriedEmberCount += spawned.Count;
            }

            RecountBuried();
            Array.Copy(_buriedByLayer, _initialByLayer, _buriedByLayer.Length);
        }

        /// <summary>Buries one specific ember (e.g. the single Heart Ember) where it says, as part of the mountain.</summary>
        public void PlaceEmber(Ember ember)
        {
            _embers[World.ChunkIndexAt(ember.Position)].Add(ember);
            BuriedEmberCount++;
            if (ember.Layer < _buriedByLayer.Length)
            {
                _buriedByLayer[ember.Layer]++;
                _initialByLayer[ember.Layer]++;
            }
        }

        /// <summary>Share of a layer's embers already taken out of the pile (0..1); 1 for a layer that had none.</summary>
        public float ClearedShare(int layer)
        {
            if (layer < 0 || layer >= _initialByLayer.Length || _initialByLayer[layer] == 0)
            {
                return 1f;
            }

            return 1f - _buriedByLayer[layer] / (float)_initialByLayer[layer];
        }

        public int InitialEmbersIn(int layer)
        {
            return layer >= 0 && layer < _initialByLayer.Length ? _initialByLayer[layer] : 0;
        }

        public int BuriedEmbersIn(int layer)
        {
            return layer >= 0 && layer < _buriedByLayer.Length ? _buriedByLayer[layer] : 0;
        }

        private void RecountBuried()
        {
            BuriedEmberCount = 0;
            Array.Clear(_buriedByLayer, 0, _buriedByLayer.Length);
            for (int i = 0; i < _embers.Length; i++)
            {
                EmberField field = _embers[i];
                BuriedEmberCount += field.Count;
                for (int e = 0; e < field.Count; e++)
                {
                    int layer = field[e].Layer;
                    if (layer < _buriedByLayer.Length)
                    {
                        _buriedByLayer[layer]++;
                    }
                }
            }
        }

        /// <summary>Scatters embers chunk by chunk; same parameters always give the same mountain.</summary>
        public void SpawnEmbers(EmberSpawnParameters parameters, IReadOnlyList<float> tierWeights)
        {
            List<Ember> spawned = new List<Ember>();
            BuriedEmberCount = 0;
            for (int i = 0; i < _embers.Length; i++)
            {
                spawned.Clear();
                EmberSpawnParameters chunkParameters = new EmberSpawnParameters(
                    parameters.EmbersPerCubicMeter, parameters.MinDensity, parameters.Seed * ChunkSeedStride + i);
                EmberSpawner.Spawn(World.GetChunk(i), chunkParameters, tierWeights, spawned);
                _embers[i].AddRange(spawned);
                BuriedEmberCount += spawned.Count;
            }

            RecountBuried();
            Array.Copy(_buriedByLayer, _initialByLayer, _buriedByLayer.Length);
        }

        /// <param name="center">World-local position (the terrain object's local space).</param>
        /// <param name="extractedEmbers">Embers inside the sphere are appended here.</param>
        /// <param name="removedByLayer">If given, cubic meters taken from each layer are added to it.</param>
        /// <param name="emberCatchMargin">Embers this far beyond the dug sphere are caught too (meters).</param>
        public DigResult Dig(Vector3 center, float radius, List<Ember> extractedEmbers, float[] removedByLayer = null,
            float emberCatchMargin = 0f)
        {
            DigResult result = StartDig(center, radius, extractedEmbers, removedByLayer, emberCatchMargin);
            CarveDig(center, radius);
            return result;
        }

        /// <summary>
        /// First half of a dig that is carved over a moment: takes the embers and counts the ash exactly as
        /// <see cref="Dig"/> would, but carves nothing. Follow with <see cref="CarveDig"/> calls of growing radius,
        /// ending with the full one, and change nothing else in between.
        /// </summary>
        public DigResult StartDig(Vector3 center, float radius, List<Ember> extractedEmbers, float[] removedByLayer,
            float emberCatchMargin)
        {
            DigResult measured = SphereDigBrush.Measure(World, center, radius, Locks, removedByLayer);

            // The sieve catches embers a little beyond the ash it takes, so one scoop on a glimmer gets its ember.
            float catchRadius = radius + Mathf.Max(0f, emberCatchMargin);
            Vector3 extent = Vector3.one * catchRadius;
            World.ChunksOverlapping(center - extent, center + extent, _chunkScratch);
            int emberCount = 0;
            int firstNew = extractedEmbers.Count;
            for (int i = 0; i < _chunkScratch.Count; i++)
            {
                int chunk = _chunkScratch[i];
                int extracted = _embers[chunk].ExtractWithin(center, catchRadius, extractedEmbers, Locks);
                if (extracted > 0)
                {
                    _embersChanged[chunk] = true;
                    _emberVersions[chunk]++;
                    emberCount += extracted;
                }
            }

            for (int i = firstNew; i < extractedEmbers.Count; i++)
            {
                int layer = extractedEmbers[i].Layer;
                if (layer < _buriedByLayer.Length)
                {
                    _buriedByLayer[layer]--;
                }
            }

            TotalAshVolume -= measured.RemovedVolume;
            BuriedEmberCount -= emberCount;
            return new DigResult(measured.RemovedVolume, emberCount, measured.BlockedLayer);
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
