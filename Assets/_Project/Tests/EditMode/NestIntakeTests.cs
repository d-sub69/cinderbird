using System.Collections.Generic;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Nest;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class NestIntakeTests
    {
        private static readonly int[] Glow = { 1, 5, 25 };
        private static readonly int[] Heat = { 1, 3, 10 };

        [Test]
        public void Add_CreditsCurrencies_AndRemembersResult()
        {
            Currencies currencies = new Currencies();
            NestIntake nest = new NestIntake(currencies, Glow, Heat);

            nest.Add(new List<Ember> { new Ember(Vector3.zero, 1), new Ember(Vector3.zero, 2) });

            Assert.That(currencies.Glow, Is.EqualTo(30));
            Assert.That(currencies.Heat, Is.EqualTo(13));
            Assert.That(nest.LastResult.EmberCount, Is.EqualTo(2));
            Assert.That(nest.LastResult.Heat, Is.EqualTo(13));
        }

        [Test]
        public void Add_Empty_ChangesNothing()
        {
            Currencies currencies = new Currencies();
            int changes = 0;
            currencies.Changed += () => changes++;

            new NestIntake(currencies, Glow, Heat).Add(new List<Ember>());

            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void FreeSpace_IsUnlimited()
        {
            Assert.That(new NestIntake(new Currencies(), Glow, Heat).FreeSpace, Is.EqualTo(int.MaxValue));
        }
    }
}
