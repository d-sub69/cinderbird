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
