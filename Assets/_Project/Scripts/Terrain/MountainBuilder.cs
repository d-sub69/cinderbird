using System;
using System.Collections.Generic;
using Cinderbird.Embers;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Generates the fresh mountain from its data: the ash, its layers and its buried embers (the Heart Ember included).
    /// Shared by the game (<see cref="VoxelTerrain"/>) and by tools that need the very same mountain without a scene,
    /// such as the pacing report.
    /// </summary>
    public static class MountainBuilder
    {
        /// <param name="layers">Optional: null builds a one-layer mountain.</param>
        /// <param name="spawn">Optional together with <paramref name="tiers"/>: without them the mountain is empty.</param>
        public static AshTerrain Build(VoxelTerrainSettings settings, LayerCatalog layers, EmberSpawnSettings spawn,
            EmberTierCatalog tiers)
        {
            VoxelWorld world = new VoxelWorld(
                settings.ChunkCells, settings.ChunksX, settings.ChunksY, settings.ChunksZ,
                settings.CellSize, settings.WorldOrigin);
            int layerCount = layers != null ? Math.Max(1, layers.Count) : 1;
            AshTerrain terrain = new AshTerrain(world, layerCount);
            terrain.Generate(settings.PileShape, layers != null ? layers.CreateLayout() : null);
            if (spawn != null && tiers != null)
            {
                SpawnEmbers(terrain, layers, spawn, tiers);
            }

            return terrain;
        }

        private static void SpawnEmbers(AshTerrain terrain, LayerCatalog layers, EmberSpawnSettings spawn, EmberTierCatalog tiers)
        {
            if (layers != null)
            {
                EmberSpawnParameters parameters = spawn.ToParameters();
                terrain.SpawnLayeredEmbers(layers.CreateSpawnTable(tiers.Count), parameters.Seed, parameters.MinDensity);
                // Exactly one Heart Ember, at the center of the innermost pocket.
                if (layers.Count > 1)
                {
                    terrain.PlaceEmber(new Ember(layers.PocketCenter, layers.HeartTierIndex, (byte)(layers.Count - 1)));
                }

                return;
            }

            // One-off at startup, so the temporary list is fine.
            List<float> weights = new List<float>();
            tiers.CopySpawnWeights(weights);
            terrain.SpawnEmbers(spawn.ToParameters(), weights);
        }
    }
}
