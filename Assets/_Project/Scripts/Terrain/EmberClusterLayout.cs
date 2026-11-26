using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Buries the ember clusters: each layer's clusters at random spots inside that layer (any depth of it), kept apart
    /// from each other, then each cluster's embers scattered through its sphere wherever it is that layer's ash.
    /// Deterministic for the same world, plans and seed.
    /// </summary>
    public static class EmberClusterLayout
    {
        // Tries per cluster to find a free spot in its layer, and per ember to land inside the cluster's ash.
        private const int SpotAttemptsPerCluster = 1500;
        private const int AttemptsPerEmber = 40;
        // Clusters keep this many radii apart (of the two), so each glows on its own.
        private const float Spacing = 1.2f;
        // Each cluster's size and ember count vary around the layer's average.
        private const float RadiusJitter = 0.3f;
        private const float EmberJitter = 0.4f;
        // A cluster's center must be solidly in the ash, not on its skin.
        private const float CenterMinDensity = 0.6f;

        /// <summary>Picks where every layer's clusters lie, and how many embers each gets.</summary>
        public static List<EmberCluster> Place(VoxelWorld world, IReadOnlyList<ClusterPlan> plans, int seed)
        {
            List<EmberCluster> clusters = new List<EmberCluster>();
            ClusterGrid grid = new ClusterGrid(MaxRadius(plans) * (1f + RadiusJitter) * 2f * Spacing);
            System.Random random = new System.Random(seed);
            ushort nextId = 1;
            for (int layer = 0; layer < plans.Count; layer++)
            {
                ClusterPlan plan = plans[layer];
                for (int c = 0; c < plan.Clusters && nextId < ushort.MaxValue; c++)
                {
                    float radius = plan.Radius * (1f + RadiusJitter * (float)(random.NextDouble() * 2.0 - 1.0));
                    int embers = Math.Max(1, (int)Math.Round(plan.EmbersPerCluster * (1f + EmberJitter * (float)(random.NextDouble() * 2.0 - 1.0))));
                    if (!TryFindSpot(world, clusters, grid, layer, radius, random, out Vector3 center))
                    {
                        continue;
                    }

                    grid.Add(center, clusters.Count);
                    clusters.Add(new EmberCluster(nextId++, center, radius, (byte)layer, embers));
                }
            }

            return clusters;
        }

        /// <summary>Buries each cluster's embers in its sphere, in its layer's ash, with its layer's tiers.</summary>
        public static void BuryEmbers(AshTerrain terrain, IReadOnlyList<EmberCluster> clusters, IReadOnlyList<ClusterPlan> plans,
            int seed, float minDensity)
        {
            VoxelWorld world = terrain.World;
            System.Random random = new System.Random(seed);
            foreach (EmberCluster cluster in clusters)
            {
                float[] weights = cluster.Layer < plans.Count ? plans[cluster.Layer].TierWeights : Array.Empty<float>();
                float totalWeight = 0f;
                foreach (float weight in weights)
                {
                    totalWeight += Math.Max(0f, weight);
                }

                if (totalWeight <= 0f)
                {
                    continue;
                }

                int placed = 0;
                int attempts = cluster.Embers * AttemptsPerEmber;
                while (placed < cluster.Embers && attempts-- > 0)
                {
                    Vector3 position = cluster.Center + RandomInSphere(random) * cluster.Radius;
                    if (world.SampleDensity(position) < minDensity || LayerAt(world, position) != cluster.Layer)
                    {
                        continue;
                    }

                    terrain.PlaceEmber(new Ember(position, PickTier(weights, totalWeight, random), cluster.Layer, cluster.Id));
                    placed++;
                }
            }
        }

        private static float MaxRadius(IReadOnlyList<ClusterPlan> plans)
        {
            float largest = 0.5f;
            foreach (ClusterPlan plan in plans)
            {
                largest = Math.Max(largest, plan.Radius);
            }

            return largest;
        }

        private static bool TryFindSpot(VoxelWorld world, List<EmberCluster> placed, ClusterGrid grid, int layer, float radius,
            System.Random random, out Vector3 center)
        {
            Vector3 min = world.Origin + Vector3.one * world.CellSize;
            Vector3 size = world.Size - Vector3.one * (2f * world.CellSize);
            for (int attempt = 0; attempt < SpotAttemptsPerCluster; attempt++)
            {
                center = min + new Vector3(size.x * (float)random.NextDouble(), size.y * (float)random.NextDouble(),
                    size.z * (float)random.NextDouble());
                if (world.SampleDensity(center) < CenterMinDensity || LayerAt(world, center) != layer ||
                    Crowded(placed, grid, center, radius))
                {
                    continue;
                }

                return true;
            }

            center = default;
            return false;
        }

        private static bool Crowded(List<EmberCluster> placed, ClusterGrid grid, Vector3 center, float radius)
        {
            foreach (int index in grid.Near(center))
            {
                EmberCluster other = placed[index];
                float apart = (other.Radius + radius) * Spacing;
                if ((other.Center - center).sqrMagnitude < apart * apart)
                {
                    return true;
                }
            }

            return false;
        }

        // Uniform in the unit sphere.
        private static Vector3 RandomInSphere(System.Random random)
        {
            while (true)
            {
                Vector3 p = new Vector3((float)(random.NextDouble() * 2.0 - 1.0), (float)(random.NextDouble() * 2.0 - 1.0),
                    (float)(random.NextDouble() * 2.0 - 1.0));
                if (p.sqrMagnitude <= 1f)
                {
                    return p;
                }
            }
        }

        private static int LayerAt(VoxelWorld world, Vector3 position)
        {
            Vector3 cell = (position - world.Origin) / world.CellSize;
            int x = Mathf.Clamp(Mathf.RoundToInt(cell.x), 0, world.SamplesX - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(cell.y), 0, world.SamplesY - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(cell.z), 0, world.SamplesZ - 1);
            return world.GetLayer(x, y, z);
        }

        private static int PickTier(float[] weights, float totalWeight, System.Random random)
        {
            float roll = (float)random.NextDouble() * totalWeight;
            int lastPickable = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                float weight = Math.Max(0f, weights[i]);
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

            return lastPickable;
        }
    }
}
