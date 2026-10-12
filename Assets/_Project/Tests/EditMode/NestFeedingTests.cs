using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Nest;
using Cinderbird.Sifting;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class NestFeedingTests
    {
        private static readonly int[] Glow = { 1, 5, 25 };
        private static readonly int[] Heat = { 1, 3, 10 };

        private static BasketContents BasketWithTiers(params int[] tiers)
        {
            BasketContents basket = new BasketContents(10);
            List<Ember> embers = new List<Ember>();
            foreach (int tier in tiers)
            {
                embers.Add(new Ember(Vector3.zero, tier));
            }

            basket.Add(embers);
            return basket;
        }

        [Test]
        public void Feed_EmptiesBasket_AndCreditsCurrencies()
        {
            BasketContents basket = BasketWithTiers(0, 1, 2);
            Currencies currencies = new Currencies();
            List<Ember> fed = new List<Ember>();

            FeedResult result = NestFeeding.Feed(basket, currencies, Glow, Heat, fed);

            Assert.That(basket.Embers, Is.Empty);
            Assert.That(result.EmberCount, Is.EqualTo(3));
            Assert.That(currencies.Glow, Is.EqualTo(31));
            Assert.That(currencies.Heat, Is.EqualTo(14));
            Assert.That(fed.Count, Is.EqualTo(3));
        }

        [Test]
        public void Feed_EmptyBasket_EarnsNothingAndRaisesNoChange()
        {
            Currencies currencies = new Currencies();
            int changes = 0;
            currencies.Changed += () => changes++;

            FeedResult result = NestFeeding.Feed(new BasketContents(10), currencies, Glow, Heat, new List<Ember>());

            Assert.That(result.EmberCount, Is.Zero);
            Assert.That(currencies.Glow, Is.Zero);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void Feed_ClearsTheFedListFirst()
        {
            List<Ember> fed = new List<Ember> { new Ember(Vector3.zero, 0) };

            NestFeeding.Feed(BasketWithTiers(2), new Currencies(), Glow, Heat, fed);

            Assert.That(fed.Count, Is.EqualTo(1));
            Assert.That(fed[0].TierIndex, Is.EqualTo(2));
        }
    }
}
