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
        public void TakeNewest_TakesFromTheEnd_InOriginalOrder()
        {
            SieveContents sieve = new SieveContents();
            List<Ember> embers = new List<Ember>();
            for (int i = 0; i < 5; i++)
            {
                embers.Add(new Ember(Vector3.zero, i));
            }

            sieve.TryAdd(0f, embers);
            List<Ember> taken = new List<Ember>();

            int count = sieve.TakeNewest(2, taken);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(taken[0].TierIndex, Is.EqualTo(3));
            Assert.That(taken[1].TierIndex, Is.EqualTo(4));
            Assert.That(sieve.Embers.Count, Is.EqualTo(3));
            Assert.That(sieve.Embers[2].TierIndex, Is.EqualTo(2));
        }

        [Test]
        public void TakeNewest_MoreThanPresent_TakesAll()
        {
            SieveContents sieve = new SieveContents();
            sieve.TryAdd(0f, Embers(2));

            int count = sieve.TakeNewest(10, new List<Ember>());

            Assert.That(count, Is.EqualTo(2));
            Assert.That(sieve.Embers, Is.Empty);
        }

        [Test]
        public void TakeNewest_FiresChangedOnlyWhenSomethingMoves()
        {
            SieveContents sieve = new SieveContents();
            sieve.TryAdd(0f, Embers(2));
            int changes = 0;
            sieve.Changed += () => changes++;

            sieve.TakeNewest(0, new List<Ember>());
            sieve.TakeNewest(1, new List<Ember>());

            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void AshFraction_TracksTheCurrentLoad()
        {
            SieveContents sieve = new SieveContents();
            Assert.That(sieve.AshFraction, Is.Zero);

            sieve.TryAdd(0.2f, Embers(0));
            Assert.That(sieve.AshFraction, Is.EqualTo(1f));

            sieve.RemoveAsh(0.05f);
            Assert.That(sieve.AshFraction, Is.EqualTo(0.75f).Within(1e-5f));

            sieve.RemoveAsh(1f);
            Assert.That(sieve.AshFraction, Is.Zero);
        }

        [Test]
        public void LoadVolume_ResetsWithEachScoop()
        {
            SieveContents sieve = new SieveContents();
            sieve.TryAdd(0.2f, Embers(0));
            sieve.RemoveAsh(1f);

            sieve.TryAdd(0.05f, Embers(0));

            Assert.That(sieve.LoadVolume, Is.EqualTo(0.05f));
            Assert.That(sieve.AshFraction, Is.EqualTo(1f));
        }

        [Test]
        public void EmberCapacity_Reached_StopsScooping()
        {
            SieveContents sieve = new SieveContents { EmberCapacity = 3 };
            sieve.TryAdd(0f, Embers(2));
            Assert.That(sieve.CanScoop, Is.True);

            sieve.TryAdd(0f, Embers(1));

            Assert.That(sieve.IsFullOfEmbers, Is.True);
            Assert.That(sieve.CanScoop, Is.False);
        }

        [Test]
        public void EmberCapacity_ALoadAlreadyOnItsWay_IsStillAccepted()
        {
            // The limit is checked before digging; a load that's pouring in must never be lost.
            SieveContents sieve = new SieveContents { EmberCapacity = 2 };
            sieve.TryAdd(0f, Embers(2));

            bool added = sieve.TryAdd(0.1f, Embers(1));

            Assert.That(added, Is.True);
            Assert.That(sieve.EmberCount, Is.EqualTo(3));
        }

        [Test]
        public void PouringOut_FreesCapacity()
        {
            SieveContents sieve = new SieveContents { EmberCapacity = 2 };
            sieve.TryAdd(0f, Embers(2));

            sieve.TakeNewest(1, new List<Ember>());

            Assert.That(sieve.CanScoop, Is.True);
        }

        [Test]
        public void TryAdd_KeepsTheLoadsColors()
        {
            SieveContents sieve = new SieveContents();
            Color ash = new Color(0.6f, 0.3f, 0.2f, 1f);
            Color dust = new Color(0.9f, 0.7f, 0.6f, 0.8f);

            sieve.TryAdd(0.1f, Embers(0), ash, dust);

            Assert.That(sieve.AshColor, Is.EqualTo(ash));
            Assert.That(sieve.DustColor, Is.EqualTo(dust));
        }

        [Test]
        public void TryAdd_NegativeAsh_Throws()
        {
            SieveContents sieve = new SieveContents();

            Assert.Throws<ArgumentOutOfRangeException>(() => sieve.TryAdd(-1f, Embers(0)));
        }

        [Test]
        public void Restore_ReplacesEverything_AndFiresChangedOnce()
        {
            SieveContents sieve = new SieveContents();
            sieve.TryAdd(0.1f, Embers(3));
            int changes = 0;
            sieve.Changed += () => changes++;
            Color ash = new Color(0.6f, 0.3f, 0.2f, 1f);
            Color dust = new Color(0.9f, 0.7f, 0.6f, 0.8f);

            sieve.Restore(0.05f, 0.2f, ash, dust, Embers(1));

            Assert.That(sieve.AshVolume, Is.EqualTo(0.05f));
            Assert.That(sieve.LoadVolume, Is.EqualTo(0.2f));
            Assert.That(sieve.AshFraction, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(sieve.AshColor, Is.EqualTo(ash));
            Assert.That(sieve.DustColor, Is.EqualTo(dust));
            Assert.That(sieve.EmberCount, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Restore_LoadSmallerThanAsh_IsRaised()
        {
            SieveContents sieve = new SieveContents();

            sieve.Restore(0.1f, 0f, Color.grey, Color.grey, Embers(0));

            Assert.That(sieve.LoadVolume, Is.EqualTo(0.1f));
            Assert.That(sieve.AshFraction, Is.EqualTo(1f));
        }
    }
}
