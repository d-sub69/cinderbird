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

            DigResult started = terrain.StartDig(center, 0.8f, new List<Ember>(), null, null, 0f);
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

            staged.StartDig(center, 0.8f, new List<Ember>(), null, null, 0f);
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
        public void GrazedEmber_BesideTheHole_ComesOutSideways_AndStillCounts()
        {
            AshTerrain terrain = CreateTerrain();
            Vector3 center = new Vector3(0f, 0.9f, 0f);
            // Level with the center, 0.15 m beyond a 0.4 m sphere: the scoop reached its side, within a 0.25 m margin.
            Ember beside = new Ember(center + Vector3.right * 0.55f, 1);
            terrain.PlaceEmber(beside);
            int countBefore = terrain.BuriedEmberCount;
            List<Ember> taken = new List<Ember>();
            List<LooseEmber> exposed = new List<LooseEmber>();

            terrain.Dig(center, 0.4f, taken, null, exposed, 0.25f);

            Assert.That(taken.Exists(e => e.Position == beside.Position), Is.False, "Not taken");
            Vector3 expected = center + Vector3.right * 0.35f;
            Assert.That(exposed.Exists(e => (e.Ember.Position - expected).magnitude < 1e-4f), Is.True, "Sideways into the hole, same height");
            Assert.That(exposed.Exists(e => e.CameFrom == beside.Position), Is.True, "Knows where it lay, to be seen coming out");
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(countBefore - taken.Count), "Exposed embers still count as on the mountain");
        }

        [Test]
        public void GrazedEmber_UnderTheHolesFloor_StaysBuried()
        {
            AshTerrain terrain = CreateTerrain();
            Vector3 center = new Vector3(0f, 0.9f, 0f);
            // 0.2 m below the bottom of the sphere: no open space at its height, so it stays put, thinly covered.
            Ember below = new Ember(center + Vector3.down * 0.6f, 1);
            terrain.PlaceEmber(below);
            List<LooseEmber> exposed = new List<LooseEmber>();

            terrain.Dig(center, 0.4f, new List<Ember>(), null, exposed, 0.25f);

            Assert.That(exposed.Exists(e => e.CameFrom == below.Position), Is.False);
            List<Ember> buried = new List<Ember>();
            terrain.CopyAllEmbers(buried);
            Assert.That(buried.Exists(e => e.Position == below.Position), Is.True);
        }

        [Test]
        public void ExposedSpot_NeverMovesUp()
        {
            Vector3 center = new Vector3(0f, 2f, 0f);
            for (float y = 1.7f; y <= 2.3f; y += 0.05f)
            {
                Vector3 ember = new Vector3(0.5f, y, 0.2f);
                if (AshTerrain.TryExposedSpot(center, 0.4f, ember, out Vector3 spot))
                {
                    Assert.That(spot.y, Is.EqualTo(ember.y), $"Ember at height {y}");
                    Assert.That(new Vector2(spot.x, spot.z).magnitude, Is.LessThan(new Vector2(ember.x, ember.z).magnitude));
                }
            }
        }

        [Test]
        public void LooseEmbers_CountUntilCollected()
        {
            AshTerrain terrain = CreateTerrain();
            int count = terrain.BuriedEmberCount;
            Ember ember = new Ember(new Vector3(0.3f, 1.2f, 0.2f), 0, 0);

            int id = terrain.AddDroppedLoose(ember);
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(count + 1));
            Assert.That(terrain.LooseEmbers.Count, Is.EqualTo(1));

            int index = terrain.IndexOfLoose(id);
            terrain.MoveLoose(index, new Vector3(0.3f, 1.0f, 0.2f));
            terrain.SetLooseRest(index, 0.05f);
            Assert.That(terrain.LooseEmbers[index].Rest, Is.EqualTo(0.05f));
            Assert.That(terrain.LooseEmbers[index].Ember.Position, Is.EqualTo(new Vector3(0.3f, 1.0f, 0.2f)));
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(count + 1), "Moving it doesn't change the count");

            Ember collected = terrain.TakeLoose(index);
            Assert.That(collected.Position, Is.EqualTo(new Vector3(0.3f, 1.0f, 0.2f)));
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(count), "Collecting counts it as taken");
        }

        [Test]
        public void Dig_TakesLooseEmbersInsideIt()
        {
            AshTerrain terrain = CreateTerrain();
            Vector3 center = new Vector3(0f, 1f, 0f);
            terrain.AddLoose(new Ember(center + Vector3.up * 0.1f, 2, 0));
            List<Ember> taken = new List<Ember>();

            terrain.Dig(center, 0.4f, taken);

            Assert.That(terrain.LooseEmbers, Is.Empty);
            Assert.That(taken.Exists(e => e.TierIndex == 2), Is.True);
        }

        [Test]
        public void ReplaceLoose_RestoresThemAndTheCounts()
        {
            AshTerrain terrain = CreateTerrain();
            int count = terrain.BuriedEmberCount;

            terrain.ReplaceLoose(new[] { new Ember(Vector3.up, 0), new Ember(Vector3.one, 1) });

            Assert.That(terrain.LooseEmbers.Count, Is.EqualTo(2));
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(count + 2));
        }

        [Test]
        public void LooseRules_FindTheSurfaceAboveOrBelowAnEmber()
        {
            AshTerrain terrain = CreateTerrain();
            VoxelWorld world = terrain.World;
            const float radius = 0.1f;
            float surface = SurfaceAtCenter(world);

            Assert.That(LooseEmberRules.TryFindSurface(world, new Vector3(0f, surface - 0.3f, 0f), radius, 3f, out float fromInside), Is.True, "Covered");
            Assert.That(fromInside, Is.EqualTo(surface).Within(0.02f));
            Assert.That(LooseEmberRules.TryFindSurface(world, new Vector3(0f, surface + radius, 0f), radius, 3f, out float fromAbove), Is.True, "Lying on top");
            Assert.That(fromAbove, Is.EqualTo(surface).Within(0.02f));
            Assert.That(LooseEmberRules.TryFindSurface(world, new Vector3(0f, surface + 1f, 0f), radius, 3f, out _), Is.False, "In the air: it should fall");
            Assert.That(LooseEmberRules.RestsOnAsh(world, new Vector3(0f, 3f, 0f), radius), Is.False);
            Assert.That(LooseEmberRules.RestsOnAsh(world, new Vector3(0f, surface + radius, 0f), radius), Is.True);
        }

        [Test]
        public void LooseRules_AshPiledOverAnEmber_LiftsItBackToItsRest()
        {
            AshTerrain terrain = CreateTerrain();
            VoxelWorld world = terrain.World;
            const float radius = 0.1f;
            float surface = SurfaceAtCenter(world);
            Vector3 covered = new Vector3(0f, surface - 0.4f, 0f);

            Assert.That(LooseEmberRules.TryFindSurface(world, covered, radius, 3f, out float found), Is.True);
            float y = LooseEmberRules.Settle(covered.y, found, radius, radius, out float rest);

            Assert.That(y, Is.EqualTo(surface + radius).Within(0.02f), "Back on top, not buried");
            Assert.That(rest, Is.EqualTo(radius));
        }

        // The top of the test mound straight above its middle.
        private static float SurfaceAtCenter(VoxelWorld world)
        {
            float surface = 0f;
            for (float y = 0f; y < 3f; y += 0.005f)
            {
                if (world.SampleDensity(new Vector3(0f, y, 0f)) > 0f)
                {
                    surface = y;
                }
            }

            return surface;
        }
    }
}
