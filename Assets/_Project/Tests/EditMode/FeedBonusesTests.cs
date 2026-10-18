using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Nest;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class FeedBonusesTests
    {
        [Test]
        public void Apply_AddsThePercent()
        {
            FeedBonuses bonuses = new FeedBonuses();
            bonuses.Set("set:a", 10f, 0f);
            int glow = 100;
            int heat = 50;

            bonuses.Apply(ref glow, ref heat);

            Assert.That(glow, Is.EqualTo(110));
            Assert.That(heat, Is.EqualTo(50));
        }

        [Test]
        public void Apply_SmallFeedings_CarryTheFractionsSoNothingIsLost()
        {
            FeedBonuses bonuses = new FeedBonuses();
            bonuses.Set("set:a", 10f, 0f);
            int total = 0;

            for (int i = 0; i < 20; i++)
            {
                int glow = 1;
                int heat = 0;
                bonuses.Apply(ref glow, ref heat);
                total += glow;
            }

            Assert.That(total, Is.EqualTo(22), "+10% of 20 single sparks is 2 extra");
        }

        [Test]
        public void Sources_AddUp_AndZeroRemovesOne()
        {
            FeedBonuses bonuses = new FeedBonuses();
            bonuses.Set("set:a", 10f, 0f);
            bonuses.Set("set:b", 25f, 5f);

            Assert.That(bonuses.GlowPercent, Is.EqualTo(35f));
            Assert.That(bonuses.HeatPercent, Is.EqualTo(5f));
            bonuses.Set("set:b", 0f, 0f);
            Assert.That(bonuses.GlowPercent, Is.EqualTo(10f));
            Assert.That(bonuses.HeatPercent, Is.Zero);
        }

        [Test]
        public void NestIntake_PaysTheBonusOnTop()
        {
            Currencies currencies = new Currencies();
            FeedBonuses bonuses = new FeedBonuses();
            bonuses.Set("set:b", 0f, 10f);
            NestIntake intake = new NestIntake(currencies, new[] { 5 }, new[] { 10 }, bonuses);

            intake.Add(new List<Ember> { new Ember(Vector3.zero, 0), new Ember(Vector3.zero, 0) });

            Assert.That(currencies.Glow, Is.EqualTo(10));
            Assert.That(currencies.Heat, Is.EqualTo(22));
            Assert.That(intake.LastResult.Heat, Is.EqualTo(22));
        }
    }
}
