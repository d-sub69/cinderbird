using System.Collections.Generic;
using Cinderbird.Sifting;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class SieveSpotTests
    {
        private const int Count = 32;
        private const float Radius = 0.17f;
        private const float Jitter = 0.25f;

        [Test]
        public void CreateSpots_ReturnsRequestedCount()
        {
            Vector2[] spots = SieveSpotLayout.CreateSpots(Count, Radius, Jitter, new System.Random(1));

            Assert.That(spots.Length, Is.EqualTo(Count));
        }

        [Test]
        public void CreateSpots_AtMaxSafeJitter_KeepMinimumDistance_ForManySeeds()
        {
            const int count = 20;
            const float emberSize = 0.05f;
            float safeJitter = SieveSpotLayout.MaxSafeJitter(count, Radius, emberSize);
            Assume.That(safeJitter, Is.GreaterThan(0f));

            for (int seed = 0; seed < 200; seed++)
            {
                Vector2[] spots = SieveSpotLayout.CreateSpots(count, Radius, safeJitter, new System.Random(seed));
                Assert.That(SieveSpotLayout.MinimumDistance(spots), Is.GreaterThanOrEqualTo(emberSize - 1e-5f),
                    $"Seed {seed} let two embers touch");
            }
        }

        [Test]
        public void MaxSafeJitter_TooManySpotsForEmberSize_IsNegative()
        {
            // 32 spots in a 0.17 m disc sit ~4.6 cm apart at best, too close for 5 cm embers.
            Assert.That(SieveSpotLayout.MaxSafeJitter(32, Radius, 0.05f), Is.LessThan(0f));
        }

        [Test]
        public void MaxSafeJitter_SmallerEmbers_AllowMoreJitter()
        {
            float forLarge = SieveSpotLayout.MaxSafeJitter(Count, Radius, 0.04f);
            float forSmall = SieveSpotLayout.MaxSafeJitter(Count, Radius, 0.03f);

            Assert.That(forSmall, Is.GreaterThan(forLarge));
        }

        [Test]
        public void CreateSpots_ZeroJitter_IsTheSameEveryTime()
        {
            Vector2[] first = SieveSpotLayout.CreateSpots(Count, Radius, 0f, new System.Random(1));
            Vector2[] second = SieveSpotLayout.CreateSpots(Count, Radius, 0f, new System.Random(2));

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void CreateSpots_StayNearTheDisc()
        {
            Vector2[] spots = SieveSpotLayout.CreateSpots(Count, Radius, Jitter, new System.Random(3));
            float maxOffset = SieveSpotLayout.NominalSpacing(Count, Radius) * Jitter * 0.5f;

            foreach (Vector2 spot in spots)
            {
                Assert.That(spot.magnitude, Is.LessThanOrEqualTo(Radius + maxOffset + 1e-5f));
            }
        }

        [Test]
        public void Assigner_GivesEverySpotExactlyOnce_ThenRunsOut()
        {
            SieveSpotAssigner assigner = new SieveSpotAssigner(Count, new System.Random(5));
            HashSet<int> taken = new HashSet<int>();

            for (int i = 0; i < Count; i++)
            {
                int spot = assigner.TakeRandom();
                Assert.That(spot, Is.InRange(0, Count - 1));
                Assert.That(taken.Add(spot), Is.True, $"Spot {spot} handed out twice");
            }

            Assert.That(assigner.TakeRandom(), Is.EqualTo(-1));
        }

        [Test]
        public void Assigner_ReleasedSpot_CanBeTakenAgain()
        {
            SieveSpotAssigner assigner = new SieveSpotAssigner(2, new System.Random(5));
            int first = assigner.TakeRandom();
            assigner.TakeRandom();

            assigner.Release(first);

            Assert.That(assigner.TakeRandom(), Is.EqualTo(first));
        }

        [Test]
        public void Assigner_OrderIsRandom()
        {
            SieveSpotAssigner assigner = new SieveSpotAssigner(Count, new System.Random(9));
            bool allInOrder = true;

            for (int i = 0; i < Count; i++)
            {
                if (assigner.TakeRandom() != i)
                {
                    allInOrder = false;
                }
            }

            Assert.That(allInOrder, Is.False);
        }
    }
}
