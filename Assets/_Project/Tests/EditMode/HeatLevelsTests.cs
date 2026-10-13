using System;
using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class HeatLevelsTests
    {
        private static readonly HeatLevels Levels = new HeatLevels(new[] { 50, 250, 1000 });

        [Test]
        public void LevelFor_CountsTheThresholdsReached()
        {
            Assert.That(Levels.LevelFor(0), Is.Zero);
            Assert.That(Levels.LevelFor(49), Is.Zero);
            Assert.That(Levels.LevelFor(50), Is.EqualTo(1));
            Assert.That(Levels.LevelFor(249), Is.EqualTo(1));
            Assert.That(Levels.LevelFor(250), Is.EqualTo(2));
            Assert.That(Levels.LevelFor(5000), Is.EqualTo(3), "Never beyond the last level");
        }

        [Test]
        public void HeatFor_AndProgress()
        {
            Assert.That(Levels.HeatFor(0), Is.Zero);
            Assert.That(Levels.HeatFor(2), Is.EqualTo(250));
            Assert.That(Levels.ProgressToNext(150), Is.EqualTo(0.5f).Within(1e-5f), "Halfway from 50 to 250");
            Assert.That(Levels.ProgressToNext(25), Is.EqualTo(0.5f).Within(1e-5f), "Halfway from 0 to 50");
            Assert.That(Levels.ProgressToNext(2000), Is.EqualTo(1f));
        }

        [Test]
        public void Thresholds_MustRise()
        {
            Assert.Throws<ArgumentException>(() => new HeatLevels(new[] { 50, 50 }));
            Assert.Throws<ArgumentException>(() => new HeatLevels(new[] { 0, 10 }));
            Assert.Throws<ArgumentException>(() => new HeatLevels(new int[0]));
        }

        [Test]
        public void UnlockRule_NeedsBothTheLevelAndTheClearedShare()
        {
            float[] cleared = { 0.95f, 0.2f, 0f };

            Assert.That(LayerUnlockRule.CanOpen(1, 2, 2, 0.9f, cleared), Is.True);
            Assert.That(LayerUnlockRule.CanOpen(1, 1, 2, 0.9f, cleared), Is.False, "Heat level too low");
            Assert.That(LayerUnlockRule.CanOpen(2, 9, 4, 0.9f, cleared), Is.False, "Layer 1 only 20% cleared");
            Assert.That(LayerUnlockRule.CanOpen(1, 2, 2, 1f, cleared), Is.False, "100% needed");
        }

        [Test]
        public void UnlockRule_ReportsWhatHoldsALayerShut()
        {
            float[] cleared = { 0.95f, 0.2f, 0f };

            float least = LayerUnlockRule.LeastClearedAbove(2, cleared, out int layer);

            Assert.That(layer, Is.EqualTo(1));
            Assert.That(least, Is.EqualTo(0.2f));
            Assert.That(LayerUnlockRule.LeastClearedAbove(0, cleared, out int none), Is.EqualTo(1f));
            Assert.That(none, Is.EqualTo(-1));
        }

        [Test]
        public void UnlockRule_ToleratesRounding()
        {
            Assert.That(LayerUnlockRule.CanOpen(1, 1, 1, 0.9f, new[] { 0.89999f, 0f }), Is.True);
        }

        // 4 × 2 × 4 m, two shells; embers placed by hand so the counts are known.
        private static AshTerrain CreateLayered()
        {
            VoxelWorld world = new VoxelWorld(8, 2, 1, 2, 0.25f, new Vector3(-2f, -0.25f, -2f));
            AshTerrain terrain = new AshTerrain(world, 2);
            terrain.Generate(new AshPileShape(1.9f, 1.8f, 0f, 0f, 1), new LayerLayout(new[] { 0f }, new Vector3(0f, 0.4f, 0f), 0.3f));
            return terrain;
        }

        [Test]
        public void Terrain_ClearedShare_FollowsDigs_AndRestores()
        {
            AshTerrain terrain = CreateLayered();
            for (int i = 0; i < 4; i++)
            {
                terrain.PlaceEmber(new Ember(new Vector3(-1f + i * 0.6f, 0.6f, 0.5f), 0, 0));
            }

            Assert.That(terrain.InitialEmbersIn(0), Is.EqualTo(4));
            Assert.That(terrain.ClearedShare(0), Is.Zero);
            Assert.That(terrain.ClearedShare(1), Is.EqualTo(1f), "A layer without embers counts as cleared");

            terrain.Dig(new Vector3(-1f, 0.6f, 0.5f), 0.2f, new List<Ember>());

            Assert.That(terrain.BuriedEmbersIn(0), Is.EqualTo(3));
            Assert.That(terrain.ClearedShare(0), Is.EqualTo(0.25f).Within(1e-5f));

            // Restoring recounts what's buried but keeps the generated totals.
            terrain.FinishRestore(new List<int>());
            Assert.That(terrain.InitialEmbersIn(0), Is.EqualTo(4));
            Assert.That(terrain.ClearedShare(0), Is.EqualTo(0.25f).Within(1e-5f));
        }
    }
}
