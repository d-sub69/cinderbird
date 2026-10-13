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
        private readonly List<int> _chunkScratch = new List<int>();

        public AshTerrain(VoxelWorld world, int layerCount = 1)
        {
            World = world;
            Locks = new LayerLocks(layerCount);
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
            TotalAshVolume = World.TotalAshVolume();
            World.MarkAllDirty();
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
        }

        /// <summary>Buries one specific ember (e.g. the single Heart Ember) where it says.</summary>
        public void PlaceEmber(Ember ember)
        {
            _embers[World.ChunkIndexAt(ember.Position)].Add(ember);
            BuriedEmberCount++;
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
        }

        /// <param name="center">World-local position (the terrain object's local space).</param>
        /// <param name="extractedEmbers">Embers inside the sphere are appended here.</param>
        /// <param name="removedByLayer">If given, cubic meters taken from each layer are added to it.</param>
        public DigResult Dig(Vector3 center, float radius, List<Ember> extractedEmbers, float[] removedByLayer = null)
        {
            DigResult carve = SphereDigBrush.Dig(World, center, radius, Locks, removedByLayer);

            Vector3 extent = Vector3.one * radius;
            World.ChunksOverlapping(center - extent, center + extent, _chunkScratch);
            int emberCount = 0;
            for (int i = 0; i < _chunkScratch.Count; i++)
            {
                emberCount += _embers[_chunkScratch[i]].ExtractWithin(center, radius, extractedEmbers, Locks);
            }

            TotalAshVolume -= carve.RemovedVolume;
            BuriedEmberCount -= emberCount;
            return new DigResult(carve.RemovedVolume, emberCount, carve.BlockedLayer);
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
