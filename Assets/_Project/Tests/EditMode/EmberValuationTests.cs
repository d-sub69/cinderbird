using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Nest;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class EmberValuationTests
    {
        // Matches the placeholder tiers: Cinder, Ember, Flame Heart.
        private static readonly int[] Glow = { 1, 5, 25 };
        private static readonly int[] Heat = { 1, 3, 10 };

        private static List<Ember> EmbersOfTiers(params int[] tiers)
        {
            List<Ember> embers = new List<Ember>();
            foreach (int tier in tiers)
            {
                embers.Add(new Ember(Vector3.zero, tier));
            }

            return embers;
        }

        [Test]
        public void Evaluate_SumsTierValues()
        {
            FeedResult result = EmberValuation.Evaluate(EmbersOfTiers(0, 0, 1, 2), Glow, Heat);

            Assert.That(result.EmberCount, Is.EqualTo(4));
            Assert.That(result.Glow, Is.EqualTo(1 + 1 + 5 + 25));
            Assert.That(result.Heat, Is.EqualTo(1 + 1 + 3 + 10));
        }

        [Test]
        public void Evaluate_UnknownTier_CountsButIsWorthNothing()
        {
            FeedResult result = EmberValuation.Evaluate(EmbersOfTiers(1, 7, -1), Glow, Heat);

            Assert.That(result.EmberCount, Is.EqualTo(3));
            Assert.That(result.Glow, Is.EqualTo(5));
            Assert.That(result.Heat, Is.EqualTo(3));
        }

        [Test]
        public void Evaluate_NoEmbers_IsZero()
        {
            FeedResult result = EmberValuation.Evaluate(EmbersOfTiers(), Glow, Heat);

            Assert.That(result.EmberCount, Is.Zero);
            Assert.That(result.Glow, Is.Zero);
            Assert.That(result.Heat, Is.Zero);
        }
    }
}
