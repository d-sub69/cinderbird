using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Sifting;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class BasketContentsTests
    {
        private static List<Ember> Embers(int count)
        {
            List<Ember> embers = new List<Ember>();
            for (int i = 0; i < count; i++)
            {
                embers.Add(new Ember(Vector3.zero, 0));
            }

            return embers;
        }

        [Test]
        public void New_IsEmptyWithFullFreeSpace()
        {
            BasketContents basket = new BasketContents(5);

            Assert.That(basket.Embers, Is.Empty);
            Assert.That(basket.FreeSpace, Is.EqualTo(5));
            Assert.That(basket.IsFull, Is.False);
        }

        [Test]
        public void Constructor_ZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BasketContents(0));
        }

        [Test]
        public void Add_UpToCapacity_FillsBasket()
        {
            BasketContents basket = new BasketContents(5);

            basket.Add(Embers(2));
            basket.Add(Embers(3));

            Assert.That(basket.Embers.Count, Is.EqualTo(5));
            Assert.That(basket.IsFull, Is.True);
        }

        [Test]
        public void Add_MoreThanFreeSpace_ThrowsAndChangesNothing()
        {
            BasketContents basket = new BasketContents(3);
            basket.Add(Embers(2));

            Assert.Throws<InvalidOperationException>(() => basket.Add(Embers(2)));
            Assert.That(basket.Embers.Count, Is.EqualTo(2));
        }

        [Test]
        public void TakeNewest_TakesFromTheEnd()
        {
            BasketContents basket = new BasketContents(5);
            basket.Add(new List<Ember> { new Ember(Vector3.zero, 0), new Ember(Vector3.zero, 1), new Ember(Vector3.zero, 2) });
            List<Ember> output = new List<Ember>();

            int taken = basket.TakeNewest(2, output);

            Assert.That(taken, Is.EqualTo(2));
            Assert.That(output[0].TierIndex, Is.EqualTo(1));
            Assert.That(output[1].TierIndex, Is.EqualTo(2));
            Assert.That(basket.Embers.Count, Is.EqualTo(1));
            Assert.That(basket.HasAsh, Is.False);
        }

        [Test]
        public void TakeAll_EmptiesBasketIntoOutput()
        {
            BasketContents basket = new BasketContents(5);
            basket.Add(Embers(3));
            List<Ember> output = new List<Ember>();

            int taken = basket.TakeAll(output);

            Assert.That(taken, Is.EqualTo(3));
            Assert.That(output.Count, Is.EqualTo(3));
            Assert.That(basket.Embers, Is.Empty);
            Assert.That(basket.FreeSpace, Is.EqualTo(5));
        }

        [Test]
        public void TakeAll_EmptyBasket_TakesNothingAndRaisesNoChange()
        {
            BasketContents basket = new BasketContents(5);
            int changes = 0;
            basket.Changed += () => changes++;

            int taken = basket.TakeAll(new List<Ember>());

            Assert.That(taken, Is.Zero);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void Changed_FiresOnAdd_ButNotForEmptyAdd()
        {
            BasketContents basket = new BasketContents(5);
            int changes = 0;
            basket.Changed += () => changes++;

            basket.Add(Embers(0));
            basket.Add(Embers(2));

            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Restore_ReplacesTheContents()
        {
            BasketContents basket = new BasketContents(5);
            basket.Add(Embers(4));

            int dropped = basket.Restore(Embers(2));

            Assert.That(dropped, Is.Zero);
            Assert.That(basket.EmberCount, Is.EqualTo(2));
        }

        [Test]
        public void Restore_MoreThanFits_KeepsWhatFits()
        {
            BasketContents basket = new BasketContents(3);

            int dropped = basket.Restore(Embers(5));

            Assert.That(dropped, Is.EqualTo(2));
            Assert.That(basket.IsFull, Is.True);
        }

        [Test]
        public void SetCapacity_ABiggerBasket_HasRoomForMore()
        {
            BasketContents basket = new BasketContents(3);
            basket.Restore(Embers(3));
            Assert.That(basket.IsFull, Is.True);
            int changes = 0;
            basket.Changed += () => changes++;

            basket.SetCapacity(5);

            Assert.That(basket.Capacity, Is.EqualTo(5));
            Assert.That(basket.FreeSpace, Is.EqualTo(2));
            Assert.That(basket.IsFull, Is.False);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void SetCapacity_BelowWhatItHolds_KeepsTheEmbers_AndIsFull()
        {
            BasketContents basket = new BasketContents(5);
            basket.Restore(Embers(4));

            basket.SetCapacity(2);

            Assert.That(basket.EmberCount, Is.EqualTo(4));
            Assert.That(basket.FreeSpace, Is.Zero);
            Assert.That(basket.IsFull, Is.True);
        }
    }
}
