using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Pacing;
using Cinderbird.Save;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class ClusterTests
    {
        private const int ChunkCells = 16;
        private const float CellSize = 0.25f;
        private static readonly float[] Spark = { 1f };

        // A 16 × 8 × 16 m world with a 6 m mound: shells at 0 / 1.5 / 3 m deep, a small pocket at the bottom.
        private static AshTerrain CreateMound()
        {
            Vector3 origin = new Vector3(-2 * ChunkCells * CellSize, -CellSize, -2 * ChunkCells * CellSize);
            VoxelWorld world = new VoxelWorld(ChunkCells, 4, 2, 4, CellSize, origin);
            AshTerrain terrain = new AshTerrain(world, 4);
            terrain.Generate(new AshPileShape(7f, 6f, 0f, 0f, 1), new LayerLayout(new[] { 0f, 1.5f, 3f }, new Vector3(0f, 0.6f, 0f), 0.5f));
            return terrain;
        }

        private static ClusterPlan[] Plans(int outerClusters)
        {
            return new[]
            {
                new ClusterPlan(outerClusters, 20, 0.6f, 0, Spark),
                new ClusterPlan(4, 20, 0.6f, 0, Spark),
                new ClusterPlan(0, 1, 0.5f, 0, Spark),
                new ClusterPlan(0, 1, 0.5f, 0, Spark),
            };
        }

        private static List<EmberCluster> Bury(AshTerrain terrain, ClusterPlan[] plans)
        {
            List<EmberCluster> clusters = EmberClusterLayout.Place(terrain.World, plans, 7);
            EmberClusterLayout.BuryEmbers(terrain, clusters, plans, 8, 0.99f);
            return clusters;
        }

        [Test]
        public void Plan_SplitsTheCountIntoClustersAndSprinkle()
        {
            ClusterPlan plan = ClusterPlan.FromCount(1000, 0.9f, 25, 0.8f, Spark);

            Assert.That(plan.Clusters, Is.EqualTo(36), "900 embers in clusters of 25");
            Assert.That(plan.Sprinkle, Is.EqualTo(100));
            Assert.That(ClusterPlan.FromCount(0, 0.9f, 25, 0.8f, Spark).Clusters, Is.Zero);
        }

        [Test]
        public void Layout_PutsEachClusterInItsLayer_ApartFromTheOthers_WithItsEmbers()
        {
            AshTerrain terrain = CreateMound();
            List<EmberCluster> clusters = Bury(terrain, Plans(8));
            List<Ember> embers = new List<Ember>();
            terrain.CopyAllEmbers(embers);

            Assert.That(clusters.Count, Is.GreaterThanOrEqualTo(10), "most of the 12 asked find room");
            for (int a = 0; a < clusters.Count; a++)
            {
                for (int b = a + 1; b < clusters.Count; b++)
                {
                    Assert.That(Vector3.Distance(clusters[a].Center, clusters[b].Center),
                        Is.GreaterThan(clusters[a].Radius + clusters[b].Radius), "clusters glow on their own");
                }
            }

            foreach (Ember ember in embers)
            {
                Assert.That(ember.Cluster, Is.GreaterThan(0), "no sprinkle asked");
                EmberCluster home = clusters.Find(c => c.Id == ember.Cluster);
                Assert.That(ember.Layer, Is.EqualTo(home.Layer));
                Assert.That(Vector3.Distance(ember.Position, home.Center), Is.LessThanOrEqualTo(home.Radius + 1e-4f));
            }
        }

        [Test]
        public void Clusters_FollowTheirEmbers_ThinningAsTheyAreDug()
        {
            AshTerrain terrain = CreateMound();
            List<EmberCluster> layout = Bury(terrain, Plans(6));
            EmberClusters clusters = new EmberClusters(layout, terrain.World.ChunkCount, new[] { 1f });
            clusters.Refresh(terrain);
            EmberCluster first = layout[0];
            ClusterState before = clusters[first.Id];

            Assert.That(before.Count, Is.EqualTo(first.Embers).Within(first.Embers / 3), "about what was buried");
            Assert.That(Vector3.Distance(before.Center, first.Center), Is.LessThan(first.Radius * 0.6f));
            Assert.That(clusters.Refresh(terrain), Is.False, "nothing changed");

            terrain.Locks.UnlockThrough(3);
            terrain.Dig(before.Center, first.Radius * 2f, new List<Ember>());
            Assert.That(clusters.Refresh(terrain), Is.True);
            Assert.That(clusters[first.Id].IsEmpty, Is.True, "dug out");
            Assert.That(clusters.TryNearest(before.Center, first.Layer, out ClusterState next), Is.True);
            Assert.That(next.Id, Is.Not.EqualTo(first.Id), "the nearest one still holding embers");
        }

        [Test]
        public void Depth_IsTheAshOverAPoint_NotTheGroundUnderIt()
        {
            AshTerrain terrain = CreateMound();
            VoxelWorld world = terrain.World;

            float deep = ClusterDepth.Probe(world, new Vector3(0f, 0.6f, 0f), 10f, out _);
            float shallow = ClusterDepth.Probe(world, new Vector3(0f, 5.5f, 0f), 10f, out _);

            Assert.That(ClusterDepth.Probe(world, new Vector3(0f, 7.5f, 0f), 10f, out _), Is.Zero, "already in the air");
            Assert.That(shallow, Is.LessThan(1f));
            Assert.That(deep, Is.GreaterThan(3f), "0.6 m above the ground in the middle: buried by the mound, not open below");
            Assert.That(ClusterDepth.Probe(world, new Vector3(0f, 0.6f, 0f), 1f, out _), Is.GreaterThan(1f), "deeper than the reach");
        }

        [Test]
        public void Glow_BrighterWhenRicher_PatchGrowsAsYouDigNear()
        {
            float poor = ClusterGlowMath.Intensity(25f, 1f, 2.5f, 0.35f, 0.5f, 6f);
            float rich = ClusterGlowMath.Intensity(400f, 1f, 2.5f, 0.35f, 0.5f, 6f);

            Assert.That(rich, Is.GreaterThan(poor * 2f));
            Assert.That(rich, Is.LessThan(poor * 16f), "richer, but not in proportion");
            Assert.That(ClusterGlowMath.Intensity(25f, 3f, 2.5f, 0.35f, 0.5f, 6f), Is.Zero, "too deep to show");
            Assert.That(ClusterGlowMath.Intensity(0f, 0f, 2.5f, 0.35f, 0.5f, 6f), Is.Zero, "emptied");
            float far = ClusterGlowMath.PatchRadius(1f, 2.4f, 2.5f, 0.3f, 0.1f);
            float near = ClusterGlowMath.PatchRadius(1f, 0f, 2.5f, 0.3f, 0.1f);
            Assert.That(far, Is.LessThan(0.35f), "a small spot from far above");
            Assert.That(near, Is.EqualTo(1f).Within(1e-4f), "the whole cluster once dug into");
        }

        [Test]
        public void Melt_RemovesTheLayersAbove_AndReleasesTheirEmbers()
        {
            AshTerrain terrain = CreateMound();
            Bury(terrain, Plans(6));
            float before = terrain.TotalAshVolume;
            int outerEmbers = terrain.BuriedEmbersIn(0);
            List<Ember> released = new List<Ember>();

            terrain.MeltLayersAbove(1, released, new List<BuriedFind>());

            Assert.That(released.Count, Is.EqualTo(outerEmbers));
            Assert.That(released.TrueForAll(e => e.Layer == 0), Is.True);
            Assert.That(terrain.ClearedShare(0), Is.EqualTo(1f), "the melted layer counts as cleared");
            Assert.That(terrain.TotalAshVolume, Is.LessThan(before * 0.8f));
            Assert.That(terrain.BuriedEmbersIn(1), Is.GreaterThan(0), "the opened layer keeps its embers");
        }

        [Test]
        public void Snapshot_KeepsEachEmbersCluster()
        {
            AshTerrain terrain = CreateMound();
            Bury(terrain, Plans(6));
            terrain.Locks.UnlockThrough(3);
            terrain.Dig(new Vector3(0f, 5f, 0f), 1f, new List<Ember>());

            TerrainSnapshot loaded = TerrainSnapshot.Decode(TerrainSnapshot.Capture(terrain).Encode());
            AshTerrain fresh = CreateMound();
            loaded.ApplyTo(fresh);

            List<Ember> embers = new List<Ember>();
            fresh.CopyAllEmbers(embers);
            Assert.That(embers.Exists(e => e.Cluster > 0), Is.True, "clusters come back from a save");
        }

        [Test]
        public void Shake_AFullSieveOfAnySizeTakesTheSameTime()
        {
            Assert.That(SieveShakeScale.For(0.4f, 0.4f), Is.EqualTo(1f));
            Assert.That(SieveShakeScale.For(0.4f, 0.8f), Is.EqualTo(8f).Within(1e-4f), "twice the radius, eight times the ash");
            Assert.That(SieveShakeScale.For(0f, 0.8f), Is.EqualTo(1f));
        }

        [Test]
        public void Simulator_OpeningALayerMeltsTheCrustIntoTheNest()
        {
            PaceLayer[] layers =
            {
                new PaceLayer("Top", 100f, new[] { 100 }, 0, 0f),
                new PaceLayer("Bottom", 100f, new[] { 100 }, 0, 0.5f),
            };
            PaceBehavior ideal = new PaceBehavior(0f, 1f, 1f, 0f, 0f, 1f, 1f, 0f, 0f, 1f, 2f);
            PaceModel model = new PaceModel(layers, new[] { 1 }, new[] { 1 }, new[] { false }, new[] { 50, 100 }, new PaceUpgrade[0],
                ideal, 100f, 4f, 0f, 99, 0f);

            PaceRun run = PaceSimulator.Run(model);

            Assert.That(run.LayerOpenedAt[1], Is.EqualTo(500.0).Within(2.0), "half the top layer");
            Assert.That(run.LevelReachedAt[1], Is.EqualTo(500.0).Within(2.0), "the other half melts into the nest at once");
            Assert.That(run.Events.Exists(e => e.Text.Contains("crust melted")), Is.True);
            Assert.That(run.FinishedAt, Is.EqualTo(1500.0).Within(3.0), "and only the bottom layer is left to dig");
        }
    }
}
