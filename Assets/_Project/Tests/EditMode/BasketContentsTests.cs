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
        public void Changed_FiresOnAdd_ButNotForEmptyAdd()
        {
            BasketContents basket = new BasketContents(5);
            int changes = 0;
            basket.Changed += () => changes++;

            basket.Add(Embers(0));
            basket.Add(Embers(2));

            Assert.That(changes, Is.EqualTo(1));
        }
    }
}
