using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class AshTerrainTests
    {
        private const int ChunkCells = 8;
        private const float CellSize = 0.25f;
        private static readonly float[] Weights = { 80f, 18f, 2f };

        // A 4 × 2 × 4 m world (2 × 1 × 2 chunks) with a mound in the middle, where all four chunks meet.
        private static AshTerrain CreateTerrain()
        {
            Vector3 origin = new Vector3(-ChunkCells * CellSize, -CellSize, -ChunkCells * CellSize);
            VoxelWorld world = new VoxelWorld(ChunkCells, 2, 1, 2, CellSize, origin);
            AshTerrain terrain = new AshTerrain(world);
            terrain.Generate(new AshPileShape(1.8f, 1.5f, 0f, 0f, 1));
            terrain.SpawnEmbers(new EmberSpawnParameters(40f, 0.99f, 5), Weights);
            return terrain;
        }

        private static int CountAll(AshTerrain terrain)
        {
            List<Ember> all = new List<Ember>();
            terrain.CopyAllEmbers(all);
            return all.Count;
        }

        [Test]
        public void Generate_SetsTheRunningAshTotal()
        {
            AshTerrain terrain = CreateTerrain();

            Assert.That(terrain.TotalAshVolume, Is.EqualTo(terrain.World.TotalAshVolume()).Within(1e-4f));
            Assert.That(terrain.TotalAshVolume, Is.GreaterThan(0f));
        }

        [Test]
        public void SpawnEmbers_CountMatchesWhatIsStored()
        {
            AshTerrain terrain = CreateTerrain();

            Assert.That(terrain.BuriedEmberCount, Is.GreaterThan(0));
            Assert.That(CountAll(terrain), Is.EqualTo(terrain.BuriedEmberCount));
        }

        [Test]
        public void SpawnEmbers_IsDeterministic()
        {
            List<Ember> first = new List<Ember>();
            List<Ember> second = new List<Ember>();

            CreateTerrain().CopyAllEmbers(first);
            CreateTerrain().CopyAllEmbers(second);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].Position, Is.EqualTo(first[i].Position));
            }
        }

        [Test]
        public void Dig_WhereChunksMeet_ExtractsFromSeveralChunks_AndKeepsTotalsRight()
        {
            AshTerrain terrain = CreateTerrain();
            float ashBefore = terrain.TotalAshVolume;
            int buriedBefore = terrain.BuriedEmberCount;
            List<Ember> extracted = new List<Ember>();

            DigResult result = terrain.Dig(new Vector3(0f, 0.5f, 0f), 0.8f, extracted);

            Assert.That(result.EmberCount, Is.EqualTo(extracted.Count));
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(buriedBefore - extracted.Count));
            Assert.That(CountAll(terrain), Is.EqualTo(terrain.BuriedEmberCount));
            Assert.That(terrain.TotalAshVolume, Is.EqualTo(ashBefore - result.RemovedVolume).Within(1e-4f));
            Assert.That(terrain.TotalAshVolume, Is.EqualTo(terrain.World.TotalAshVolume()).Within(1e-3f));

            bool fromNegativeX = false;
            bool fromPositiveX = false;
            foreach (Ember ember in extracted)
            {
                fromNegativeX |= ember.Position.x < 0f;
                fromPositiveX |= ember.Position.x >= 0f;
            }

            Assert.That(fromNegativeX && fromPositiveX, Is.True, "Expected embers from chunks on both sides of the seam");
        }

        [Test]
        public void Dig_SameSpotTwice_SecondFindsNothing()
        {
            AshTerrain terrain = CreateTerrain();
            terrain.Dig(new Vector3(0f, 0.5f, 0f), 0.8f, new List<Ember>());

            DigResult second = terrain.Dig(new Vector3(0f, 0.5f, 0f), 0.8f, new List<Ember>());

            Assert.That(second.RemovedVolume, Is.Zero);
            Assert.That(second.EmberCount, Is.Zero);
        }

        [Test]
        public void Dig_MarksOnlyTouchedChunksDirty()
        {
            AshTerrain terrain = CreateTerrain();
            List<int> dirty = new List<int>();
            terrain.World.TakeDirtyChunks(dirty);

            // Low on the mound's slope inside the (-x, -z) chunk, away from every seam, where there is ash to remove.
            terrain.Dig(new Vector3(-1f, 0.1f, -1f), 0.3f, new List<Ember>());
            terrain.World.TakeDirtyChunks(dirty);

            Assert.That(dirty, Is.EquivalentTo(new[] { terrain.World.ChunkIndex(0, 0, 0) }));
        }
    }
}
