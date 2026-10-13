using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class AshLayerTests
    {
        private const int ChunkCells = 8;
        private const float CellSize = 0.25f;
        private const int TierCount = 6;

        // Shells start at 0 / 0.5 / 1.0 m below the surface; layer 3 is a pocket at the bottom center.
        private static readonly float[] BandStarts = { 0f, 0.5f, 1f };
        private static readonly Vector3 PocketCenter = new Vector3(0f, 0.4f, 0f);
        private const float PocketRadius = 0.3f;

        private static LayerLayout CreateLayout()
        {
            return new LayerLayout(BandStarts, PocketCenter, PocketRadius);
        }

        // A 4 × 2 × 4 m world (2 × 1 × 2 chunks) with a 1.8 m tall mound, layered, all four chunks meeting at the center.
        private static AshTerrain CreateLayeredTerrain()
        {
            Vector3 origin = new Vector3(-ChunkCells * CellSize, -CellSize, -ChunkCells * CellSize);
            VoxelWorld world = new VoxelWorld(ChunkCells, 2, 1, 2, CellSize, origin);
            AshTerrain terrain = new AshTerrain(world, 4);
            terrain.Generate(new AshPileShape(1.9f, 1.8f, 0f, 0f, 1), CreateLayout());
            return terrain;
        }

        private static LayerSpawnTable CreateTable()
        {
            float[][] weights =
            {
                new float[] { 100, 0, 0, 0, 0, 0 },
                new float[] { 0, 100, 0, 0, 0, 0 },
                new float[] { 0, 0, 100, 0, 0, 0 },
                new float[TierCount],
            };

            return new LayerSpawnTable(new[] { 40f, 40f, 40f, 0f }, weights);
        }

        [Test]
        public void Layout_AssignsShellsByDepth()
        {
            LayerLayout layout = CreateLayout();
            const float surface = 2f;

            Assert.That(layout.LayerAt(new Vector3(1f, 1.9f, 1f), surface), Is.EqualTo(0));
            Assert.That(layout.LayerAt(new Vector3(1f, 1.3f, 1f), surface), Is.EqualTo(1));
            Assert.That(layout.LayerAt(new Vector3(1f, 0.6f, 1f), surface), Is.EqualTo(2));
        }

        [Test]
        public void Layout_PocketOverridesShells()
        {
            LayerLayout layout = CreateLayout();

            Assert.That(layout.LayerAt(PocketCenter, 2f), Is.EqualTo(layout.PocketLayer));
            Assert.That(layout.PocketLayer, Is.EqualTo(3));
            Assert.That(layout.LayerCount, Is.EqualTo(4));
        }

        [Test]
        public void Locks_OnlyOutermostStartsOpen_AndOpenInOrder()
        {
            LayerLocks locks = new LayerLocks(3);

            Assert.That(locks.IsUnlocked(0), Is.True);
            Assert.That(locks.IsUnlocked(1), Is.False);
            Assert.That(locks.UnlockNext(), Is.EqualTo(1));
            Assert.That(locks.DeepestUnlocked, Is.EqualTo(1));
            Assert.That(locks.UnlockNext(), Is.EqualTo(2));
            Assert.That(locks.UnlockNext(), Is.EqualTo(-1));
        }

        [Test]
        public void Locks_UnknownLayers_CountAsOpen()
        {
            LayerLocks locks = new LayerLocks(2);

            Assert.That(locks.IsUnlocked(-1), Is.True);
            Assert.That(locks.IsUnlocked(7), Is.True);
        }

        [Test]
        public void Generate_LayersMatchOnBothSidesOfSeams()
        {
            AshTerrain terrain = CreateLayeredTerrain();
            VoxelWorld world = terrain.World;

            for (int i = 0; i < world.ChunkCount; i++)
            {
                VoxelGrid chunk = world.GetChunk(i);
                Vector3Int offset = world.ChunkSampleOffset(i);
                for (int z = 0; z < chunk.SamplesZ; z++)
                {
                    for (int y = 0; y < chunk.SamplesY; y++)
                    {
                        for (int x = 0; x < chunk.SamplesX; x++)
                        {
                            Assert.That(chunk.GetLayer(x, y, z), Is.EqualTo(world.GetLayer(x + offset.x, y + offset.y, z + offset.z)));
                        }
                    }
                }
            }
        }

        [Test]
        public void Dig_StopsAtALockedLayer_ButTakesOpenAshBeside_It()
        {
            AshTerrain terrain = CreateLayeredTerrain();
            // Near the top: the sphere reaches from the open crust down into the locked layer below.
            Vector3 center = new Vector3(0f, 1.5f, 0f);

            DigResult result = terrain.Dig(center, 0.6f, new List<Ember>());

            Assert.That(result.RemovedAnyAsh, Is.True, "Open crust should be dug");
            Assert.That(result.WasBlocked, Is.True);
            Assert.That(result.BlockedLayer, Is.EqualTo(1));

            // Every locked sample inside the sphere still holds its ash.
            VoxelWorld world = terrain.World;
            for (int z = 0; z < world.SamplesZ; z++)
            {
                for (int y = 0; y < world.SamplesY; y++)
                {
                    for (int x = 0; x < world.SamplesX; x++)
                    {
                        bool inside = (world.SamplePosition(x, y, z) - center).magnitude < 0.6f;
                        if (inside && world.GetLayer(x, y, z) > 0)
                        {
                            Assert.That(world.GetDensity(x, y, z), Is.GreaterThan(0f), $"Locked ash at ({x},{y},{z}) was removed");
                        }
                    }
                }
            }
        }

        [Test]
        public void Dig_ReportsHowMuchCameFromEachLayer()
        {
            AshTerrain terrain = CreateLayeredTerrain();
            terrain.Locks.UnlockNext();
            float[] removedByLayer = new float[4];

            DigResult result = terrain.Dig(new Vector3(0f, 1.5f, 0f), 0.6f, new List<Ember>(), removedByLayer);

            Assert.That(removedByLayer[0], Is.GreaterThan(0f), "Some ash came from the crust");
            Assert.That(removedByLayer[1], Is.GreaterThan(0f), "Some ash came from the second layer");
            Assert.That(removedByLayer[0] + removedByLayer[1] + removedByLayer[2] + removedByLayer[3],
                Is.EqualTo(result.RemovedVolume).Within(1e-4f));
        }

        [Test]
        public void Dig_AfterUnlocking_TakesTheDeeperLayer()
        {
            AshTerrain terrain = CreateLayeredTerrain();
            Vector3 center = new Vector3(0f, 1.5f, 0f);
            terrain.Dig(center, 0.6f, new List<Ember>());
            terrain.Locks.UnlockNext();

            DigResult second = terrain.Dig(center, 0.6f, new List<Ember>());

            Assert.That(second.RemovedAnyAsh, Is.True);
        }

        [Test]
        public void SpawnLayered_EachEmberUsesItsLayersTiers()
        {
            AshTerrain terrain = CreateLayeredTerrain();
            terrain.SpawnLayeredEmbers(CreateTable(), 3, 0.99f);
            List<Ember> embers = new List<Ember>();
            terrain.CopyAllEmbers(embers);

            Assert.That(embers.Count, Is.GreaterThan(0));
            HashSet<int> layersSeen = new HashSet<int>();
            foreach (Ember ember in embers)
            {
                // The table maps layer 0 → tier 0, 1 → 1, 2 → 2, and the pocket holds none.
                Assert.That(ember.TierIndex, Is.EqualTo(ember.Layer), "Tier doesn't match the layer's table");
                Assert.That(ember.Layer, Is.Not.EqualTo(3), "The pocket layer should spawn no random embers");
                layersSeen.Add(ember.Layer);
            }

            Assert.That(layersSeen.Count, Is.GreaterThanOrEqualTo(2), "Expected embers in several layers");
        }

        [Test]
        public void Embers_InALockedLayer_StayBuried()
        {
            AshTerrain terrain = CreateLayeredTerrain();
            terrain.PlaceEmber(new Ember(new Vector3(0f, 1.1f, 0f), 1, 1));
            List<Ember> extracted = new List<Ember>();

            terrain.Dig(new Vector3(0f, 1.1f, 0f), 0.4f, extracted);

            Assert.That(extracted, Is.Empty);
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(1));
        }

        [Test]
        public void PlaceEmber_AddsExactlyOne()
        {
            AshTerrain terrain = CreateLayeredTerrain();

            terrain.PlaceEmber(new Ember(PocketCenter, 5, 3));
            List<Ember> all = new List<Ember>();
            terrain.CopyAllEmbers(all);

            Assert.That(all.Count, Is.EqualTo(1));
            Assert.That(all[0].TierIndex, Is.EqualTo(5));
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(1));
        }

        [Test]
        public void Mesh_VertexColorsAreTheCutLayersTint()
        {
            AshTerrain terrain = CreateLayeredTerrain();
            Color[] tints = { Color.red, Color.green, Color.blue, Color.yellow };
            terrain.Locks.UnlockNext();
            terrain.Dig(new Vector3(0f, 1.5f, 0f), 0.6f, new List<Ember>());

            using MarchingCubes mesher = new MarchingCubes();
            mesher.SetLayerTints(tints);
            MeshBuffers buffers = new MeshBuffers();
            HashSet<Color> seen = new HashSet<Color>();
            for (int i = 0; i < terrain.World.ChunkCount; i++)
            {
                mesher.Polygonize(terrain.World, i, buffers);
                Assert.That(buffers.Colors.Count, Is.EqualTo(buffers.Vertices.Count));
                foreach (Color color in buffers.Colors)
                {
                    Assert.That(System.Array.IndexOf(tints, color), Is.GreaterThanOrEqualTo(0), "Color isn't one of the layer tints");
                    seen.Add(color);
                }
            }

            Assert.That(seen, Does.Contain(Color.red), "Surface should show the crust");
            Assert.That(seen, Does.Contain(Color.green), "The cut should expose the second layer");
        }
    }
}
