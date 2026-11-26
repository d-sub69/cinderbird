using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Generates the fresh mountain from its data: the ash, its layers, its glowing ember clusters, the embers sprinkled
    /// between them and the Heart Ember. Shared by the game (<see cref="VoxelTerrain"/>) and by tools that need the very
    /// same mountain without a scene, such as the pacing report.
    /// </summary>
    public static class MountainBuilder
    {
        // Random spots that measure each layer's volume for the sprinkle rates (within about a percent).
        private const int VolumeSamples = 160000;
        private const int VolumeSampleSeed = 11;

        /// <param name="layers">Optional: null builds a one-layer mountain (and no clusters).</param>
        /// <param name="spawn">Optional together with <paramref name="tiers"/>: without them the mountain is empty.</param>
        /// <param name="clusters">The clusters as they were buried; empty without layers or embers.</param>
        public static AshTerrain Build(VoxelTerrainSettings settings, LayerCatalog layers, EmberSpawnSettings spawn,
            EmberTierCatalog tiers, out IReadOnlyList<EmberCluster> clusters)
        {
            VoxelWorld world = new VoxelWorld(
                settings.ChunkCells, settings.ChunksX, settings.ChunksY, settings.ChunksZ,
                settings.CellSize, settings.WorldOrigin);
            int layerCount = layers != null ? Math.Max(1, layers.Count) : 1;
            AshTerrain terrain = new AshTerrain(world, layerCount);
            terrain.Generate(settings.PileShape, layers != null ? layers.CreateLayout() : null);
            clusters = Array.Empty<EmberCluster>();
            if (spawn == null || tiers == null)
            {
                return terrain;
            }

            if (layers == null)
            {
                // One-off at startup, so the temporary list is fine.
                List<float> weights = new List<float>();
                tiers.CopySpawnWeights(weights);
                terrain.SpawnEmbers(spawn.ToParameters(), weights);
                return terrain;
            }

            EmberSpawnParameters parameters = spawn.ToParameters();
            ClusterPlan[] plans = layers.CreateClusterPlans(tiers.Count);
            terrain.SpawnLayeredEmbers(SprinkleTable(plans, SampleLayerVolumes(world, layers.Count)), parameters.Seed, parameters.MinDensity);
            List<EmberCluster> placed = EmberClusterLayout.Place(world, plans, layers.ClusterSeed);
            EmberClusterLayout.BuryEmbers(terrain, placed, plans, layers.ClusterSeed + 1, parameters.MinDensity);
            clusters = placed;

            // Exactly one Heart Ember, at the center of the innermost pocket.
            if (layers.Count > 1)
            {
                terrain.PlaceEmber(new Ember(layers.PocketCenter, layers.HeartTierIndex, (byte)(layers.Count - 1)));
            }

            return terrain;
        }

        /// <summary>Cubic meters of ash in each layer (every layer counted, locked or not), with the game's dig measure.</summary>
        public static float[] MeasureLayerVolumes(AshTerrain terrain, AshPileShape shape)
        {
            LayerLocks all = new LayerLocks(terrain.Locks.Count);
            all.UnlockThrough(all.Count - 1);
            float[] volumes = new float[all.Count];
            Vector3 center = new Vector3(0f, shape.Height * 0.5f, 0f);
            float radius = Mathf.Sqrt(shape.Radius * shape.Radius + shape.Height * shape.Height * 0.25f) + 2f;
            SphereDigBrush.Measure(terrain.World, center, radius, all, volumes);
            return volumes;
        }

        /// <summary>Cubic meters of ash in each layer, from random samples: quick enough for start-up, within about a percent.</summary>
        public static float[] SampleLayerVolumes(VoxelWorld world, int layerCount)
        {
            int[] hits = new int[layerCount];
            System.Random random = new System.Random(VolumeSampleSeed);
            int maxX = world.SamplesX - 2;
            int maxY = world.SamplesY - 2;
            int maxZ = world.SamplesZ - 2;
            for (int i = 0; i < VolumeSamples; i++)
            {
                int x = 1 + random.Next(maxX);
                int y = 1 + random.Next(maxY);
                int z = 1 + random.Next(maxZ);
                if (world.GetDensity(x, y, z) <= 0f)
                {
                    continue;
                }

                int layer = world.GetLayer(x, y, z);
                if (layer < layerCount)
                {
                    hits[layer]++;
                }
            }

            // Each sample stands for an equal share of the box the samples were drawn from.
            double box = (double)maxX * maxY * maxZ * world.CellSize * world.CellSize * world.CellSize;
            float[] volumes = new float[layerCount];
            for (int layer = 0; layer < layerCount; layer++)
            {
                volumes[layer] = (float)(box * hits[layer] / VolumeSamples);
            }

            return volumes;
        }

        // The sprinkle is spread evenly through each layer.
        private static LayerSpawnTable SprinkleTable(ClusterPlan[] plans, float[] volumes)
        {
            float[] rates = new float[plans.Length];
            float[][] weights = new float[plans.Length][];
            for (int layer = 0; layer < plans.Length; layer++)
            {
                rates[layer] = plans[layer].Sprinkle / Math.Max(1e-3f, volumes[layer]);
                weights[layer] = plans[layer].TierWeights;
            }

            return new LayerSpawnTable(rates, weights);
        }
    }
}
