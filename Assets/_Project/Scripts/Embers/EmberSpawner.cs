using System;
using System.Collections.Generic;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Scatters embers uniformly through the ash of a grid, picking tiers by weight.
    /// Same grid + parameters + weights always gives the same embers.
    /// </summary>
    public static class EmberSpawner
    {
        // Rejection sampling gives up after this many tries per ember, so a nearly empty grid can't loop forever.
        private const int MaxAttemptsPerEmber = 200;

        /// <summary>
        /// Scatters embers through a grid whose samples carry ash layers, using each layer's own density and tier
        /// weights. Candidate spots are drawn at the densest layer's rate and kept in proportion to the rate of the
        /// layer they land in, which spawns every layer at its own rate in one pass.
        /// </summary>
        public static void SpawnLayered(VoxelGrid grid, LayerSpawnTable table, int seed, float minDensity, List<Ember> output)
        {
            float maxRate = table.MaxEmbersPerCubicMeter;
            int candidates = Mathf.RoundToInt(maxRate * grid.TotalAshVolume());
            if (candidates <= 0)
            {
                return;
            }

            System.Random random = new System.Random(seed);
            Vector3 min = grid.Origin;
            Vector3 size = new Vector3(grid.CellsX, grid.CellsY, grid.CellsZ) * grid.CellSize;
            int attemptsLeft = candidates * MaxAttemptsPerEmber;
            int found = 0;

            while (found < candidates && attemptsLeft-- > 0)
            {
                Vector3 position = min + new Vector3(
                    size.x * (float)random.NextDouble(),
                    size.y * (float)random.NextDouble(),
                    size.z * (float)random.NextDouble());

                if (grid.SampleDensity(position) < minDensity)
                {
                    continue;
                }

                found++;
                byte layer = NearestLayer(grid, position);
                // Drawn even when rejected, so the random sequence (and thus the mountain) doesn't depend on layer rates.
                float keep = (float)random.NextDouble() * maxRate;
                if (keep >= table.EmbersPerCubicMeter(layer))
                {
                    continue;
                }

                int tier = PickTier(table.TierWeights(layer), table.TotalWeight(layer), random);
                output.Add(new Ember(position, tier, layer));
            }
        }

        private static byte NearestLayer(VoxelGrid grid, Vector3 position)
        {
            Vector3 cell = (position - grid.Origin) / grid.CellSize;
            int x = Mathf.Clamp(Mathf.RoundToInt(cell.x), 0, grid.CellsX);
            int y = Mathf.Clamp(Mathf.RoundToInt(cell.y), 0, grid.CellsY);
            int z = Mathf.Clamp(Mathf.RoundToInt(cell.z), 0, grid.CellsZ);
            return grid.GetLayer(x, y, z);
        }

        public static void Spawn(VoxelGrid grid, EmberSpawnParameters parameters, IReadOnlyList<float> tierWeights, List<Ember> output)
        {
            float totalWeight = 0f;
            for (int i = 0; i < tierWeights.Count; i++)
            {
                totalWeight += Mathf.Max(0f, tierWeights[i]);
            }

            if (totalWeight <= 0f)
            {
                return;
            }

            int targetCount = Mathf.RoundToInt(parameters.EmbersPerCubicMeter * grid.TotalAshVolume());
            if (targetCount <= 0)
            {
                return;
            }

            System.Random random = new System.Random(parameters.Seed);
            // The whole grid: in a multi-chunk world a chunk's edges are seams, not air. Positions are half-open
            // ([min, min + size)), so an ember on a seam belongs to exactly one chunk.
            Vector3 min = grid.Origin;
            Vector3 size = new Vector3(grid.CellsX, grid.CellsY, grid.CellsZ) * grid.CellSize;

            int spawned = 0;
            int attemptsLeft = targetCount * MaxAttemptsPerEmber;
            while (spawned < targetCount && attemptsLeft-- > 0)
            {
                Vector3 position = min + new Vector3(
                    size.x * (float)random.NextDouble(),
                    size.y * (float)random.NextDouble(),
                    size.z * (float)random.NextDouble());

                if (grid.SampleDensity(position) < parameters.MinDensity)
                {
                    continue;
                }

                int tier = PickTier(tierWeights, totalWeight, random);
                output.Add(new Ember(position, tier));
                spawned++;
            }
        }

        private static int PickTier(IReadOnlyList<float> weights, float totalWeight, System.Random random)
        {
            float roll = (float)random.NextDouble() * totalWeight;
            int lastPickable = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                float weight = Mathf.Max(0f, weights[i]);
                if (weight <= 0f)
                {
                    continue;
                }

                lastPickable = i;
                if (roll < weight)
                {
                    return i;
                }

                roll -= weight;
            }

            // Float rounding can leave the roll just past the end; fall back to the last valid tier.
            return lastPickable;
        }
    }
}
