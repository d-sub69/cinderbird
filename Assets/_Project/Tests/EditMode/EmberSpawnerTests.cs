using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class EmberSpawnerTests
    {
        private const int Cells = 16;
        private const float CellSize = 0.25f;
        private const float MinDensity = 0.99f;

        private static readonly float[] DefaultWeights = { 80f, 18f, 2f };

        private static List<Ember> SpawnInBall(float perCubicMeter, IReadOnlyList<float> weights, int seed = 7)
        {
            VoxelGrid grid = TerrainTestGrids.Ball(Cells, CellSize, 1.5f, out _);
            List<Ember> embers = new List<Ember>();
            EmberSpawner.Spawn(grid, new EmberSpawnParameters(perCubicMeter, MinDensity, seed), weights, embers);
            return embers;
        }

        [Test]
        public void Spawn_SameSeed_GivesSameEmbers()
        {
            List<Ember> first = SpawnInBall(20f, DefaultWeights);
            List<Ember> second = SpawnInBall(20f, DefaultWeights);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].Position, Is.EqualTo(first[i].Position));
                Assert.That(second[i].TierIndex, Is.EqualTo(first[i].TierIndex));
            }
        }

        [Test]
        public void Spawn_DifferentSeed_GivesDifferentEmbers()
        {
            List<Ember> first = SpawnInBall(20f, DefaultWeights, seed: 1);
            List<Ember> second = SpawnInBall(20f, DefaultWeights, seed: 2);

            Assert.That(second[0].Position, Is.Not.EqualTo(first[0].Position));
        }

        [Test]
        public void Spawn_EmbersAreDeepInsideAsh()
        {
            VoxelGrid grid = TerrainTestGrids.Ball(Cells, CellSize, 1.5f, out _);
            List<Ember> embers = new List<Ember>();

            EmberSpawner.Spawn(grid, new EmberSpawnParameters(20f, MinDensity, 7), DefaultWeights, embers);

            Assert.That(embers, Is.Not.Empty);
            foreach (Ember ember in embers)
            {
                Assert.That(grid.SampleDensity(ember.Position), Is.GreaterThanOrEqualTo(MinDensity));
            }
        }

        [Test]
        public void Spawn_CountMatchesDensityTimesVolume()
        {
            VoxelGrid grid = TerrainTestGrids.Ball(Cells, CellSize, 1.5f, out _);
            List<Ember> embers = new List<Ember>();
            const float perCubicMeter = 10f;

            EmberSpawner.Spawn(grid, new EmberSpawnParameters(perCubicMeter, MinDensity, 7), DefaultWeights, embers);

            Assert.That(embers.Count, Is.EqualTo(Mathf.RoundToInt(perCubicMeter * grid.TotalAshVolume())));
        }

        [Test]
        public void Spawn_ZeroWeightTier_IsNeverPicked()
        {
            List<Ember> embers = SpawnInBall(50f, new[] { 1f, 0f, 1f });

            Assert.That(embers, Is.Not.Empty);
            foreach (Ember ember in embers)
            {
                Assert.That(ember.TierIndex, Is.Not.EqualTo(1));
            }
        }

        [Test]
        public void Spawn_WeightsRoughlyRespected()
        {
            List<Ember> embers = SpawnInBall(100f, new[] { 3f, 1f });
            int tierZero = 0;
            foreach (Ember ember in embers)
            {
                if (ember.TierIndex == 0)
                {
                    tierZero++;
                }
            }

            float share = (float)tierZero / embers.Count;
            Assert.That(share, Is.EqualTo(0.75f).Within(0.1f));
        }

        [Test]
        public void Spawn_NoWeights_SpawnsNothing()
        {
            List<Ember> embers = SpawnInBall(20f, new float[0]);

            Assert.That(embers, Is.Empty);
        }

        [Test]
        public void Spawn_EmptyGrid_SpawnsNothing()
        {
            VoxelGrid grid = new VoxelGrid(8, 8, 8, 0.25f, Vector3.zero);
            List<Ember> embers = new List<Ember>();

            EmberSpawner.Spawn(grid, new EmberSpawnParameters(20f, MinDensity, 7), DefaultWeights, embers);

            Assert.That(embers, Is.Empty);
        }
    }
}
