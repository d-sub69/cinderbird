using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Pacing
{
    /// <summary>
    /// Measures a freshly built mountain for the pacing simulator, with the game's own dig code so the numbers are the
    /// game's: the ash in each layer, the embers in each layer by tier, and how much one scoop of a given size takes.
    /// Measures only, never carves: the mountain stays as it was built.
    /// </summary>
    public static class MountainSurvey
    {
        /// <summary>Cubic meters of ash in each layer (every layer counted, locked or not).</summary>
        public static float[] LayerVolumes(AshTerrain terrain, float pileRadius, float pileHeight)
        {
            LayerLocks all = new LayerLocks(terrain.Locks.Count);
            all.UnlockThrough(all.Count - 1);
            float[] volumes = new float[all.Count];
            Vector3 center = new Vector3(0f, pileHeight * 0.5f, 0f);
            float radius = Mathf.Sqrt(pileRadius * pileRadius + pileHeight * pileHeight * 0.25f) + 2f;
            SphereDigBrush.Measure(terrain.World, center, radius, all, volumes);
            return volumes;
        }

        /// <summary>Embers buried in each layer, per tier: [layer][tier].</summary>
        public static int[][] EmbersByLayer(AshTerrain terrain, int layerCount, int tierCount)
        {
            int[][] counts = new int[layerCount][];
            for (int layer = 0; layer < layerCount; layer++)
            {
                counts[layer] = new int[tierCount];
            }

            List<Ember> embers = new List<Ember>(terrain.BuriedEmberCount);
            terrain.CopyAllEmbers(embers);
            foreach (Ember ember in embers)
            {
                if (ember.Layer < layerCount && ember.TierIndex >= 0 && ember.TierIndex < tierCount)
                {
                    counts[ember.Layer][ember.TierIndex]++;
                }
            }

            return counts;
        }

        /// <summary>
        /// Average cubic meters one scoop takes, aimed as a player standing at the mountain's foot would: at points spread
        /// around it, looking a little down at the slope, pushed in as the scoop pushes in.
        /// </summary>
        /// <param name="penetration">How far the scoop pushes in, as a share of its radius.</param>
        public static float ScoopVolume(AshTerrain terrain, float scoopRadius, float penetration, float pileRadius,
            int samples = 200, int seed = 3)
        {
            LayerLocks all = new LayerLocks(terrain.Locks.Count);
            all.UnlockThrough(all.Count - 1);
            System.Random random = new System.Random(seed);
            double total = 0.0;
            int count = 0;
            for (int i = 0; i < samples; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                float height = 0.8f + (float)random.NextDouble() * 1.2f;
                Vector3 inward = new Vector3(-Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
                Vector3 direction = (inward + Vector3.down * 0.5f).normalized;
                Vector3 start = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (pileRadius + 2f) + Vector3.up * (height + 1.5f);
                if (!TryHit(terrain.World, start, direction, pileRadius * 2f, out Vector3 hit))
                {
                    continue;
                }

                DigResult result = SphereDigBrush.Measure(terrain.World, hit + direction * (scoopRadius * penetration),
                    scoopRadius, all, null);
                total += result.RemovedVolume;
                count++;
            }

            return count > 0 ? (float)(total / count) : 0f;
        }

        private static bool TryHit(VoxelWorld world, Vector3 start, Vector3 direction, float maxDistance, out Vector3 hit)
        {
            const float step = 0.02f;
            hit = start;
            for (float travelled = 0f; travelled < maxDistance; travelled += step)
            {
                if (world.SampleDensity(hit) > 0f)
                {
                    return true;
                }

                hit += direction * step;
            }

            return false;
        }
    }
}
