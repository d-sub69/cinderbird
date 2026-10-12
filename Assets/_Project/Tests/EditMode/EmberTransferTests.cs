using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Sifting;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class EmberTransferTests
    {
        private readonly List<Ember> _buffer = new List<Ember>();

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
                embers.Add(new Ember(Vector3.zero, 99));
            }

            basket.Add(embers);
            return basket;
        }

        [Test]
        public void Tip_AllFit_MovesEverything()
        {
            SieveContents sieve = SieveWith(3);
            BasketContents basket = BasketWith(10, 0);

            TipOutcome outcome = EmberTransfer.Tip(sieve, basket, _buffer, out int moved);

            Assert.That(outcome, Is.EqualTo(TipOutcome.Tipped));
            Assert.That(moved, Is.EqualTo(3));
            Assert.That(sieve.Embers, Is.Empty);
            Assert.That(basket.Embers.Count, Is.EqualTo(3));
        }

        [Test]
        public void Tip_WithAshRemaining_IsRefusedAndMovesNothing()
        {
            SieveContents sieve = SieveWith(3, ash: 0.1f);
            BasketContents basket = BasketWith(10, 0);

            TipOutcome outcome = EmberTransfer.Tip(sieve, basket, _buffer, out int moved);

            Assert.That(outcome, Is.EqualTo(TipOutcome.AshRemaining));
            Assert.That(moved, Is.Zero);
            Assert.That(sieve.Embers.Count, Is.EqualTo(3));
            Assert.That(basket.Embers, Is.Empty);
        }

        [Test]
        public void Tip_EmptySieve_IsRefused()
        {
            TipOutcome outcome = EmberTransfer.Tip(SieveWith(0), BasketWith(10, 0), _buffer, out int moved);

            Assert.That(outcome, Is.EqualTo(TipOutcome.NoEmbers));
            Assert.That(moved, Is.Zero);
        }

        [Test]
        public void Tip_FullBasket_IsRefused()
        {
            SieveContents sieve = SieveWith(2);

            TipOutcome outcome = EmberTransfer.Tip(sieve, BasketWith(4, 4), _buffer, out int moved);

            Assert.That(outcome, Is.EqualTo(TipOutcome.BasketFull));
            Assert.That(moved, Is.Zero);
            Assert.That(sieve.Embers.Count, Is.EqualTo(2));
        }

        [Test]
        public void Tip_PartialRoom_MovesNewestAndKeepsOldestInPlace()
        {
            SieveContents sieve = SieveWith(5);
            BasketContents basket = BasketWith(10, 8);

            TipOutcome outcome = EmberTransfer.Tip(sieve, basket, _buffer, out int moved);

            Assert.That(outcome, Is.EqualTo(TipOutcome.Tipped));
            Assert.That(moved, Is.EqualTo(2));
            Assert.That(basket.IsFull, Is.True);
            // The three oldest stay at their original indices.
            Assert.That(sieve.Embers.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(sieve.Embers[i].TierIndex, Is.EqualTo(i));
            }

            Assert.That(basket.Embers[8].TierIndex, Is.EqualTo(3));
            Assert.That(basket.Embers[9].TierIndex, Is.EqualTo(4));
        }

        [Test]
        public void Tip_LeavesBufferEmpty()
        {
            EmberTransfer.Tip(SieveWith(3), BasketWith(10, 0), _buffer, out _);

            Assert.That(_buffer, Is.Empty);
        }
    }
}
