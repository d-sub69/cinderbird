using System.Collections.Generic;
using Cinderbird.Offerings;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class OfferingCollectionTests
    {
        private static void Find(OfferingCollection collection, int index)
        {
            collection.Uncover(index);
            collection.PickUp(index);
        }

        [Test]
        public void New_EverythingIsStillBuried()
        {
            OfferingCollection collection = new OfferingCollection(3);

            Assert.That(collection.StateOf(0), Is.EqualTo(OfferingState.Buried));
            Assert.That(collection.Place(0), Is.False, "nothing to place before it's found");
            Assert.That(collection.PickUp(0), Is.False, "nothing to pick up before a dig uncovers it");
        }

        [Test]
        public void UncoveredOffering_LiesOnThePileUntilPickedUp()
        {
            OfferingCollection collection = new OfferingCollection(1);

            Assert.That(collection.Uncover(0), Is.True);
            Assert.That(collection.StateOf(0), Is.EqualTo(OfferingState.OnPile));
            Assert.That(collection.Uncover(0), Is.False, "uncovered only once");
            Assert.That(collection.Place(0), Is.False, "it must be picked up first");
            Assert.That(collection.PickUp(0), Is.True);
            Assert.That(collection.StateOf(0), Is.EqualTo(OfferingState.InCollection));
        }

        [Test]
        public void PickedUpOffering_GoesOnTheShelfAndBackOff()
        {
            OfferingCollection collection = new OfferingCollection(2);

            Find(collection, 1);
            Assert.That(collection.Place(1), Is.True);
            Assert.That(collection.StateOf(1), Is.EqualTo(OfferingState.OnShelf));
            Assert.That(collection.Sell(1), Is.False, "take it off the shelf before selling it");
            Assert.That(collection.TakeOff(1), Is.True);
            Assert.That(collection.StateOf(1), Is.EqualTo(OfferingState.InCollection));
        }

        [Test]
        public void SoldOffering_CanBeBoughtBack_NothingIsLostForGood()
        {
            OfferingCollection collection = new OfferingCollection(1);
            Find(collection, 0);

            Assert.That(collection.Sell(0), Is.True);
            Assert.That(collection.StateOf(0), Is.EqualTo(OfferingState.Sold));
            Assert.That(collection.BuyBack(0), Is.True);
            Assert.That(collection.StateOf(0), Is.EqualTo(OfferingState.InCollection));
        }

        [Test]
        public void Set_IsCompleteOnlyWithEveryMemberOnTheShelf()
        {
            OfferingCollection collection = new OfferingCollection(4);
            int[] set = { 0, 2 };
            Find(collection, 0);
            Find(collection, 2);
            collection.Place(0);

            Assert.That(collection.AllOnShelf(set), Is.False);
            Assert.That(collection.CountOnShelf(set), Is.EqualTo(1));
            collection.Place(2);
            Assert.That(collection.AllOnShelf(set), Is.True);
            Assert.That(collection.AllOnShelf(new int[0]), Is.False, "an empty set is never complete");
        }

        [Test]
        public void Restore_SetsEveryStateAndTellsListenersOnce()
        {
            OfferingCollection collection = new OfferingCollection(3);
            List<int> changes = new List<int>();
            collection.Changed += changes.Add;

            collection.Restore(new[] { OfferingState.OnShelf, OfferingState.Sold });

            Assert.That(collection.StateOf(0), Is.EqualTo(OfferingState.OnShelf));
            Assert.That(collection.StateOf(1), Is.EqualTo(OfferingState.Sold));
            Assert.That(collection.StateOf(2), Is.EqualTo(OfferingState.Buried), "not in the save: still buried");
            Assert.That(changes, Is.EqualTo(new[] { -1 }));
        }

        [Test]
        public void Placement_PointsTheRightWayFromTheMiddle()
        {
            Vector3 north = new OfferingPlacement(0f, 0.5f, 0.3f).HorizontalPosition(20f);
            Vector3 east = new OfferingPlacement(90f, 1f, 0.3f).HorizontalPosition(20f);

            Assert.That(north.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(north.z, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(east.x, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(east.z, Is.EqualTo(0f).Within(1e-4f));
        }
    }
}
