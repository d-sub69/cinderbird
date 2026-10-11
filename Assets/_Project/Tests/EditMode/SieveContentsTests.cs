using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Sifting;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class SieveContentsTests
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
        public void New_IsEmptyAndCanScoop()
        {
            SieveContents sieve = new SieveContents();

            Assert.That(sieve.AshVolume, Is.Zero);
            Assert.That(sieve.Embers, Is.Empty);
            Assert.That(sieve.CanScoop, Is.True);
        }

        [Test]
        public void TryAdd_StoresAshAndEmbers()
        {
            SieveContents sieve = new SieveContents();

            bool added = sieve.TryAdd(0.15f, Embers(2));

            Assert.That(added, Is.True);
            Assert.That(sieve.AshVolume, Is.EqualTo(0.15f));
            Assert.That(sieve.Embers.Count, Is.EqualTo(2));
        }

        [Test]
        public void TryAdd_WhileAshRemains_IsRefusedAndChangesNothing()
        {
            SieveContents sieve = new SieveContents();
            sieve.TryAdd(0.15f, Embers(1));

            bool added = sieve.TryAdd(0.2f, Embers(3));

            Assert.That(added, Is.False);
            Assert.That(sieve.CanScoop, Is.False);
            Assert.That(sieve.AshVolume, Is.EqualTo(0.15f));
            Assert.That(sieve.Embers.Count, Is.EqualTo(1));
        }

        [Test]
        public void RemoveAsh_AllOfIt_AllowsScoopingAgain_AndKeepsEmbers()
        {
            SieveContents sieve = new SieveContents();
            sieve.TryAdd(0.15f, Embers(2));

            sieve.RemoveAsh(sieve.AshVolume);
            bool added = sieve.TryAdd(0.1f, Embers(1));

            Assert.That(added, Is.True);
            Assert.That(sieve.Embers.Count, Is.EqualTo(3));
        }

        [Test]
        public void RemoveAsh_MoreThanPresent_RemovesOnlyWhatIsThere()
        {
            SieveContents sieve = new SieveContents();
            sieve.TryAdd(0.1f, Embers(0));

            float removed = sieve.RemoveAsh(1f);

            Assert.That(removed, Is.EqualTo(0.1f));
            Assert.That(sieve.AshVolume, Is.Zero);
        }

        [Test]
        public void Changed_FiresOnAddAndRemove()
        {
            SieveContents sieve = new SieveContents();
            int changes = 0;
            sieve.Changed += () => changes++;

            sieve.TryAdd(0.1f, Embers(1));
            sieve.RemoveAsh(0.05f);

            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void Changed_DoesNotFireWhenNothingHappens()
        {
            SieveContents sieve = new SieveContents();
            sieve.TryAdd(0.1f, Embers(0));
            int changes = 0;
            sieve.Changed += () => changes++;

            sieve.TryAdd(0.1f, Embers(1));
            sieve.RemoveAsh(0f);

            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void TryAdd_NegativeAsh_Throws()
        {
            SieveContents sieve = new SieveContents();

            Assert.Throws<ArgumentOutOfRangeException>(() => sieve.TryAdd(-1f, Embers(0)));
        }
    }
}
