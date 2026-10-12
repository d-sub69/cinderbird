using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Nest;
using Cinderbird.Sifting;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class EmberTransferTests
    {
        private static readonly int[] Glow = { 1, 5, 25 };
        private static readonly int[] Heat = { 1, 3, 10 };

        private readonly List<Ember> _moved = new List<Ember>();

        // Embers get distinct tier indices 0..count-1 so tests can tell which ones moved.
        private static SieveContents SieveWith(int emberCount, float ash = 0f)
        {
            List<Ember> embers = new List<Ember>();
            for (int i = 0; i < emberCount; i++)
            {
                embers.Add(new Ember(Vector3.zero, i));
            }

            SieveContents sieve = new SieveContents();
            sieve.TryAdd(ash, embers);
            return sieve;
        }

        private static BasketContents BasketWith(int capacity, int alreadyInside)
        {
            BasketContents basket = new BasketContents(capacity);
            List<Ember> embers = new List<Ember>();
            for (int i = 0; i < alreadyInside; i++)
            {
                embers.Add(new Ember(Vector3.zero, i % 3));
            }

            basket.Add(embers);
            return basket;
        }

        [Test]
        public void SieveIntoBasket_AllFit_MovesEverything()
        {
            SieveContents sieve = SieveWith(3);
            BasketContents basket = BasketWith(10, 0);

            PourOutcome outcome = EmberTransfer.Pour(sieve, basket, _moved);

            Assert.That(outcome, Is.EqualTo(PourOutcome.Poured));
            Assert.That(_moved.Count, Is.EqualTo(3));
            Assert.That(sieve.Embers, Is.Empty);
            Assert.That(basket.Embers.Count, Is.EqualTo(3));
        }

        [Test]
        public void SieveWithAsh_IsRefusedAndMovesNothing()
        {
            SieveContents sieve = SieveWith(3, ash: 0.1f);
            BasketContents basket = BasketWith(10, 0);

            PourOutcome outcome = EmberTransfer.Pour(sieve, basket, _moved);

            Assert.That(outcome, Is.EqualTo(PourOutcome.AshRemaining));
            Assert.That(_moved, Is.Empty);
            Assert.That(sieve.Embers.Count, Is.EqualTo(3));
            Assert.That(basket.Embers, Is.Empty);
        }

        [Test]
        public void EmptySource_IsRefused()
        {
            PourOutcome outcome = EmberTransfer.Pour(SieveWith(0), BasketWith(10, 0), _moved);

            Assert.That(outcome, Is.EqualTo(PourOutcome.NoEmbers));
        }

        [Test]
        public void FullTarget_IsRefused()
        {
            SieveContents sieve = SieveWith(2);

            PourOutcome outcome = EmberTransfer.Pour(sieve, BasketWith(4, 4), _moved);

            Assert.That(outcome, Is.EqualTo(PourOutcome.TargetFull));
            Assert.That(sieve.Embers.Count, Is.EqualTo(2));
        }

        [Test]
        public void PartialRoom_MovesNewestAndKeepsOldestInPlace()
        {
            SieveContents sieve = SieveWith(5);
            BasketContents basket = BasketWith(10, 8);

            PourOutcome outcome = EmberTransfer.Pour(sieve, basket, _moved);

            Assert.That(outcome, Is.EqualTo(PourOutcome.Poured));
            Assert.That(_moved.Count, Is.EqualTo(2));
            Assert.That(basket.IsFull, Is.True);
            Assert.That(sieve.Embers.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(sieve.Embers[i].TierIndex, Is.EqualTo(i));
            }

            Assert.That(_moved[0].TierIndex, Is.EqualTo(3));
            Assert.That(_moved[1].TierIndex, Is.EqualTo(4));
        }

        [Test]
        public void SieveIntoNest_CreditsCurrencies()
        {
            Currencies currencies = new Currencies();
            NestIntake nest = new NestIntake(currencies, Glow, Heat);

            PourOutcome outcome = EmberTransfer.Pour(SieveWith(3), nest, _moved);

            Assert.That(outcome, Is.EqualTo(PourOutcome.Poured));
            Assert.That(currencies.Glow, Is.EqualTo(31));
            Assert.That(currencies.Heat, Is.EqualTo(14));
        }

        [Test]
        public void BasketIntoNest_EmptiesTheBasket()
        {
            BasketContents basket = BasketWith(10, 6);
            Currencies currencies = new Currencies();

            PourOutcome outcome = EmberTransfer.Pour(basket, new NestIntake(currencies, Glow, Heat), _moved);

            Assert.That(outcome, Is.EqualTo(PourOutcome.Poured));
            Assert.That(basket.Embers, Is.Empty);
            Assert.That(_moved.Count, Is.EqualTo(6));
            Assert.That(currencies.Heat, Is.EqualTo(2 * (1 + 3 + 10)));
        }

        [Test]
        public void SieveWithAsh_IntoNest_IsRefused()
        {
            Currencies currencies = new Currencies();

            PourOutcome outcome = EmberTransfer.Pour(SieveWith(2, ash: 0.1f), new NestIntake(currencies, Glow, Heat), _moved);

            Assert.That(outcome, Is.EqualTo(PourOutcome.AshRemaining));
            Assert.That(currencies.Glow, Is.Zero);
        }

        [Test]
        public void Pour_ClearsTheMovedListFirst()
        {
            _moved.Add(new Ember(Vector3.zero, 9));

            EmberTransfer.Pour(SieveWith(0), BasketWith(10, 0), _moved);

            Assert.That(_moved, Is.Empty);
        }
    }
}
