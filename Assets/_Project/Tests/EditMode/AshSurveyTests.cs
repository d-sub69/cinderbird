using System.Collections.Generic;
using Cinderbird.Helpers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class AshSurveyTests
    {
        private const float Cell = 0.25f;

        // 8 × 8 m; the floor (local y = 0) is sample 1.
        private static VoxelWorld CreateWorld()
        {
            return new VoxelWorld(16, 2, 1, 2, Cell, new Vector3(0f, -Cell, 0f));
        }

        // Ash up to a height over a rectangle of sample columns; samples below `layerSplit` are layer 1, the rest layer 0.
        private static void Fill(VoxelWorld world, int x0, int z0, int x1, int z1, float height, float layerSplit = -1f)
        {
            for (int z = Mathf.Max(1, z0); z <= Mathf.Min(world.SamplesZ - 2, z1); z++)
            {
                for (int x = Mathf.Max(1, x0); x <= Mathf.Min(world.SamplesX - 2, x1); x++)
                {
                    for (int y = 1; y < world.SamplesY - 1; y++)
                    {
                        float sampleY = world.SamplePosition(x, y, z).y;
                        world.SetDensity(x, y, z, Mathf.Clamp((height - sampleY) / Cell, -1f, 1f));
                        world.SetLayer(x, y, z, (byte)(sampleY < layerSplit ? 1 : 0));
                    }
                }
            }
        }

        private static LayerLocks AllOpen()
        {
            LayerLocks locks = new LayerLocks(2);
            locks.UnlockThrough(1);
            return locks;
        }

        [Test]
        public void Nearest_OfTheLayer_FromWhereSheIs()
        {
            VoxelWorld world = CreateWorld();
            Fill(world, 4, 4, 6, 6, 1f);
            Fill(world, 26, 26, 28, 28, 1f);
            AshSurvey survey = new AshSurvey(0.5f, 0.15f, 1.5f);

            Assert.That(survey.TryFindNearest(world, new LayerLocks(1), new Vector3(6.5f, 0f, 6.5f), 0, null, out Vector3 spot, out _), Is.True);
            Assert.That(spot.x, Is.GreaterThan(6f), "the far corner patch is the near one from there");
        }

        [Test]
        public void AmongTheNearOnes_TheLowestFirst()
        {
            VoxelWorld world = CreateWorld();
            // Right beside her a 2 m step; one meter farther a 0.5 m step: both near, the low one first.
            Fill(world, 9, 8, 10, 10, 2f);
            Fill(world, 13, 8, 14, 10, 0.5f);
            AshSurvey survey = new AshSurvey(0.5f, 0.15f, 1.5f);

            survey.TryFindNearest(world, new LayerLocks(1), new Vector3(1.75f, 0f, 2.25f), 0, null, out Vector3 spot, out _);

            Assert.That(spot.y, Is.EqualTo(0.5f).Within(0.15f));
        }

        [Test]
        public void FarLowAsh_DoesNotDrawHerAway()
        {
            VoxelWorld world = CreateWorld();
            Fill(world, 9, 8, 10, 10, 2f);
            Fill(world, 28, 8, 30, 10, 0.5f);
            AshSurvey survey = new AshSurvey(0.5f, 0.15f, 1.5f);

            survey.TryFindNearest(world, new LayerLocks(1), new Vector3(1.75f, 0f, 2.25f), 0, null, out Vector3 spot, out _);

            Assert.That(spot.x, Is.LessThan(3f), "the near ash, though the far ash is lower");
        }

        [Test]
        public void OnlyTheLayerAsked()
        {
            VoxelWorld world = CreateWorld();
            // Near: the inner layer (all below the split). Far: the outer layer on top.
            Fill(world, 4, 4, 6, 6, 0.5f, 2f);
            Fill(world, 26, 26, 28, 28, 1.5f, 0.6f);
            AshSurvey survey = new AshSurvey(0.5f, 0.15f, 1.5f);

            survey.TryFindNearest(world, AllOpen(), new Vector3(1f, 0f, 1f), 0, null, out Vector3 outer, out _);
            survey.TryFindNearest(world, AllOpen(), new Vector3(7f, 0f, 7f), 1, null, out Vector3 inner, out _);

            Assert.That(outer.x, Is.GreaterThan(6f), "the outer layer, far as it is");
            Assert.That(inner.x, Is.LessThan(2f), "the inner layer, far as it is");
        }

        [Test]
        public void AshThatAppearsAfterAnEmptySearch_IsFound()
        {
            VoxelWorld world = CreateWorld();
            AshSurvey survey = new AshSurvey(0.5f, 0.15f, 1.5f);
            Assert.That(survey.HasOpenAsh(world, new LayerLocks(1), new Vector3(2f, 0f, 2f), 0, null), Is.False, "an empty mountain");

            Fill(world, 20, 20, 24, 24, 1f);

            Assert.That(survey.HasOpenAsh(world, new LayerLocks(1), new Vector3(2f, 0f, 2f), 0, null), Is.True, "the chunk changed, so it is looked at again");
        }

        [Test]
        public void SealedLayerAndThinAsh_AreNoWork()
        {
            VoxelWorld world = CreateWorld();
            // Open ash under 0.2 m thick (one sample) over a sealed layer.
            Fill(world, 1, 1, 15, 15, 0.8f, 0.6f);
            LayerLocks locks = new LayerLocks(2);

            Assert.That(AshSurvey.TryOpenAsh(world, locks, 2f, 2f, 0.2f, out _, out _, out _), Is.False);

            locks.UnlockThrough(1);

            Assert.That(AshSurvey.TryOpenAsh(world, locks, 2f, 2f, 0.2f, out _, out _, out _), Is.True, "the layer opened");
        }

        [Test]
        public void SkipsExcludedSpots_AndReportsNoneLeft()
        {
            VoxelWorld world = CreateWorld();
            Fill(world, 8, 8, 8, 8, 1f);
            AshSurvey survey = new AshSurvey(0.25f, 0.15f, 1.5f);
            Assert.That(survey.TryFindNearest(world, new LayerLocks(1), new Vector3(2f, 0f, 2f), 0, null, out _, out int key), Is.True);

            HashSet<int> excluded = new HashSet<int> { key };

            Assert.That(survey.HasOpenAsh(world, new LayerLocks(1), new Vector3(2f, 0f, 2f), 0, excluded), Is.False);
        }

        [Test]
        public void Assign_OnOpenAsh_WorksThatLayer()
        {
            VoxelWorld world = CreateWorld();
            Fill(world, 8, 8, 12, 12, 1f, 0.6f);
            Fill(world, 20, 20, 24, 24, 0.5f, 2f);

            Assert.That(AssignRules.Check(world, AllOpen(), new Vector3(2.5f, 1f, 2.5f), true, 3f, 0.25f, out int outer), Is.EqualTo(AssignCheck.Valid));
            Assert.That(outer, Is.EqualTo(0));
            Assert.That(AssignRules.Check(world, AllOpen(), new Vector3(5.5f, 0.5f, 5.5f), true, 3f, 0.25f, out int inner), Is.EqualTo(AssignCheck.Valid));
            Assert.That(inner, Is.EqualTo(1));
        }

        [Test]
        public void Assign_OnSealedAsh_IsRefused()
        {
            VoxelWorld world = CreateWorld();
            Fill(world, 20, 20, 24, 24, 0.5f, 2f);

            Assert.That(AssignRules.Check(world, new LayerLocks(2), new Vector3(5.5f, 0.5f, 5.5f), true, 3f, 0.25f, out _),
                Is.EqualTo(AssignCheck.Sealed));
        }

        [Test]
        public void Assign_OnTheGround_NearThePileOnly()
        {
            VoxelWorld world = CreateWorld();
            Fill(world, 8, 8, 12, 12, 1f);

            Assert.That(AssignRules.Check(world, new LayerLocks(1), new Vector3(4.5f, 0f, 2.5f), false, 3f, 0.25f, out int layer),
                Is.EqualTo(AssignCheck.Valid));
            Assert.That(layer, Is.EqualTo(AssignRules.OutermostLayer));
            Assert.That(AssignRules.Check(world, new LayerLocks(1), new Vector3(7.5f, 0f, 7.5f), false, 3f, 0.25f, out _),
                Is.EqualTo(AssignCheck.NoAshNearby));
        }
    }
}
