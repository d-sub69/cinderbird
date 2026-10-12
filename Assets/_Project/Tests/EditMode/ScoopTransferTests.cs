using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Sifting;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class ScoopTransferTests
    {
        private const float Duration = 0.45f;

        private static List<Ember> Embers(int count)
        {
            List<Ember> embers = new List<Ember>();
            for (int i = 0; i < count; i++)
            {
                embers.Add(new Ember(Vector3.zero, i));
            }

            return embers;
        }

        [Test]
        public void New_IsIdleWithZeroProgress()
        {
            ScoopTransfer transfer = new ScoopTransfer();

            Assert.That(transfer.IsActive, Is.False);
            Assert.That(transfer.Progress, Is.Zero);
        }

        [Test]
        public void TryBegin_StartsAndRefusesASecondLoad()
        {
            ScoopTransfer transfer = new ScoopTransfer();

            Assert.That(transfer.TryBegin(0.15f, Embers(2), Duration), Is.True);
            Assert.That(transfer.TryBegin(0.2f, Embers(1), Duration), Is.False);
            Assert.That(transfer.Volume, Is.EqualTo(0.15f));
            Assert.That(transfer.Embers.Count, Is.EqualTo(2));
        }

        [Test]
        public void TryBegin_CopiesEmbers_SoTheCallerCanReuseItsList()
        {
            ScoopTransfer transfer = new ScoopTransfer();
            List<Ember> source = Embers(3);

            transfer.TryBegin(0.1f, source, Duration);
            source.Clear();

            Assert.That(transfer.Embers.Count, Is.EqualTo(3));
        }

        [Test]
        public void Tick_ProgressesAndReportsCompletion()
        {
            ScoopTransfer transfer = new ScoopTransfer();
            transfer.TryBegin(0.1f, Embers(0), Duration);

            bool doneHalfway = transfer.Tick(Duration * 0.5f);
            Assert.That(doneHalfway, Is.False);
            Assert.That(transfer.Progress, Is.EqualTo(0.5f).Within(1e-5f));

            bool doneAtEnd = transfer.Tick(Duration * 0.5f);
            Assert.That(doneAtEnd, Is.True);
            Assert.That(transfer.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void HandOverTo_PutsAshAndEmbersIntoTheSieve_AndGoesIdle()
        {
            ScoopTransfer transfer = new ScoopTransfer();
            SieveContents sieve = new SieveContents();
            transfer.TryBegin(0.15f, Embers(2), Duration);
            transfer.Tick(Duration);

            bool handedOver = transfer.HandOverTo(sieve);

            Assert.That(handedOver, Is.True);
            Assert.That(sieve.AshVolume, Is.EqualTo(0.15f));
            Assert.That(sieve.Embers.Count, Is.EqualTo(2));
            Assert.That(transfer.IsActive, Is.False);
            Assert.That(transfer.Progress, Is.Zero);
        }

        [Test]
        public void HandOverTo_EarlyStillDeliversTheWholeLoad()
        {
            ScoopTransfer transfer = new ScoopTransfer();
            SieveContents sieve = new SieveContents();
            transfer.TryBegin(0.15f, Embers(1), Duration);
            transfer.Tick(Duration * 0.1f);

            transfer.HandOverTo(sieve);

            Assert.That(sieve.AshVolume, Is.EqualTo(0.15f));
            Assert.That(sieve.Embers.Count, Is.EqualTo(1));
        }

        [Test]
        public void HandOverTo_WhenIdle_DoesNothing()
        {
            SieveContents sieve = new SieveContents();

            Assert.That(new ScoopTransfer().HandOverTo(sieve), Is.False);
            Assert.That(sieve.AshVolume, Is.Zero);
        }

        [Test]
        public void ZeroDuration_CompletesOnFirstTick()
        {
            ScoopTransfer transfer = new ScoopTransfer();
            transfer.TryBegin(0.1f, Embers(0), 0f);

            Assert.That(transfer.Progress, Is.EqualTo(1f));
            Assert.That(transfer.Tick(0f), Is.True);
        }

        [Test]
        public void NewLoadAfterHandOver_IsAccepted()
        {
            ScoopTransfer transfer = new ScoopTransfer();
            transfer.TryBegin(0.1f, Embers(1), Duration);
            transfer.HandOverTo(new SieveContents());

            Assert.That(transfer.TryBegin(0.2f, Embers(0), Duration), Is.True);
        }

        [Test]
        public void TryBegin_NegativeVolume_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoopTransfer().TryBegin(-1f, Embers(0), Duration));
        }
    }
}
