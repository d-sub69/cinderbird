using System;
using Cinderbird.Economy;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class CurrenciesTests
    {
        [Test]
        public void New_StartsAtZero()
        {
            Currencies currencies = new Currencies();

            Assert.That(currencies.Glow, Is.Zero);
            Assert.That(currencies.Heat, Is.Zero);
        }

        [Test]
        public void Add_Accumulates()
        {
            Currencies currencies = new Currencies();

            currencies.Add(5, 3);
            currencies.Add(2, 1);

            Assert.That(currencies.Glow, Is.EqualTo(7));
            Assert.That(currencies.Heat, Is.EqualTo(4));
        }

        [Test]
        public void Add_Negative_ThrowsAndChangesNothing()
        {
            Currencies currencies = new Currencies();
            currencies.Add(5, 5);

            Assert.Throws<ArgumentOutOfRangeException>(() => currencies.Add(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => currencies.Add(0, -1));
            Assert.That(currencies.Glow, Is.EqualTo(5));
            Assert.That(currencies.Heat, Is.EqualTo(5));
        }

        [Test]
        public void Changed_FiresOncePerAdd_AndNotForZero()
        {
            Currencies currencies = new Currencies();
            int changes = 0;
            currencies.Changed += () => changes++;

            currencies.Add(0, 0);
            currencies.Add(3, 2);

            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Restore_SetsBoth_AndFiresChangedAndRestored()
        {
            Currencies currencies = new Currencies();
            currencies.Add(5, 3);
            int changes = 0;
            int restores = 0;
            currencies.Changed += () => changes++;
            currencies.Restored += () => restores++;

            currencies.Restore(120, 40);

            Assert.That(currencies.Glow, Is.EqualTo(120));
            Assert.That(currencies.Heat, Is.EqualTo(40));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(restores, Is.EqualTo(1));
        }

        [Test]
        public void Restore_Negative_Throws()
        {
            Currencies currencies = new Currencies();

            Assert.Throws<ArgumentOutOfRangeException>(() => currencies.Restore(-1, 0));
        }
    }
}
