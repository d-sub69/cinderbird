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

        [Test]
        public void StartDig_TakesEmbersAndCountsAsh_ButCarvesNothing()
        {
            AshTerrain terrain = CreateTerrain();
            AshTerrain reference = CreateTerrain();
            Vector3 center = new Vector3(0f, 0.5f, 0f);
            float volumeBefore = terrain.World.TotalAshVolume();

            DigResult started = terrain.StartDig(center, 0.8f, new List<Ember>(), null, 0f);
            DigResult dug = reference.Dig(center, 0.8f, new List<Ember>());

            Assert.That(started.RemovedVolume, Is.EqualTo(dug.RemovedVolume).Within(1e-5f));
            Assert.That(started.EmberCount, Is.EqualTo(dug.EmberCount));
            Assert.That(terrain.World.TotalAshVolume(), Is.EqualTo(volumeBefore).Within(1e-4f), "Nothing carved yet");
            Assert.That(terrain.TotalAshVolume, Is.EqualTo(reference.TotalAshVolume).Within(1e-4f), "The running total already counts it");
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(reference.BuriedEmberCount));
        }

        [Test]
        public void CarvingInGrowingStages_EndsExactlyLikeOneDig()
        {
            AshTerrain staged = CreateTerrain();
            AshTerrain once = CreateTerrain();
            Vector3 center = new Vector3(0.3f, 0.6f, -0.2f);

            staged.StartDig(center, 0.8f, new List<Ember>(), null, 0f);
            for (int stage = 1; stage <= 5; stage++)
            {
                staged.CarveDig(center, 0.8f * Mathf.Pow(stage / 5f, 1f / 3f));
            }

            once.Dig(center, 0.8f, new List<Ember>());

            VoxelWorld a = staged.World;
            VoxelWorld b = once.World;
            for (int z = 0; z < a.SamplesZ; z++)
            {
                for (int y = 0; y < a.SamplesY; y++)
                {
                    for (int x = 0; x < a.SamplesX; x++)
                    {
                        Assert.That(a.GetDensity(x, y, z), Is.EqualTo(b.GetDensity(x, y, z)), $"Sample ({x},{y},{z})");
                    }
                }
            }
        }

        [Test]
        public void CatchMargin_TakesEmbersJustBeyondTheHole()
        {
            AshTerrain terrain = CreateTerrain();
            Vector3 center = new Vector3(0f, 0.9f, 0f);
            // 0.2 m below the bottom of a 0.4 m sphere: missed by the carve, within a 0.25 m catch margin.
            Ember justBelow = new Ember(center + Vector3.down * 0.6f, 1);
            terrain.PlaceEmber(justBelow);
            List<Ember> withoutMargin = new List<Ember>();
            List<Ember> withMargin = new List<Ember>();

            AshTerrain other = CreateTerrain();
            other.PlaceEmber(justBelow);
            other.Dig(center, 0.4f, withoutMargin);
            terrain.Dig(center, 0.4f, withMargin, null, 0.25f);

            Assert.That(withoutMargin.Exists(e => e.Position == justBelow.Position), Is.False);
            Assert.That(withMargin.Exists(e => e.Position == justBelow.Position), Is.True);
        }
    }
}
