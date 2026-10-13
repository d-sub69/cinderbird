using System;
using Cinderbird.Player;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class InventoryTests
    {
        private static Inventory<string> WithItems(int capacity, params string[] items)
        {
            Inventory<string> inventory = new Inventory<string>(capacity);
            foreach (string item in items)
            {
                inventory.TryAdd(item, out _);
            }

            return inventory;
        }

        [Test]
        public void New_IsEmptyWithFirstSlotActive()
        {
            Inventory<string> inventory = new Inventory<string>(5);

            Assert.That(inventory.Capacity, Is.EqualTo(5));
            Assert.That(inventory.ActiveSlot, Is.Zero);
            Assert.That(inventory.Active, Is.Null);
        }

        [Test]
        public void Constructor_ZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory<string>(0));
        }

        [Test]
        public void TryAdd_FillsSlotsInOrder()
        {
            Inventory<string> inventory = WithItems(5, "sieve", "basket");

            Assert.That(inventory[0], Is.EqualTo("sieve"));
            Assert.That(inventory[1], Is.EqualTo("basket"));
            Assert.That(inventory.Active, Is.EqualTo("sieve"));
        }

        [Test]
        public void TryAdd_WhenFull_Fails()
        {
            Inventory<string> inventory = WithItems(2, "a", "b");

            bool added = inventory.TryAdd("c", out int slot);

            Assert.That(added, Is.False);
            Assert.That(slot, Is.EqualTo(-1));
        }

        [Test]
        public void TryAdd_SameItemTwice_Fails()
        {
            Inventory<string> inventory = WithItems(5, "sieve");

            Assert.That(inventory.TryAdd("sieve", out int slot), Is.False);
            Assert.That(slot, Is.Zero);
        }

        [Test]
        public void Remove_EmptiesSlot_AndHandsIfItWasActive()
        {
            Inventory<string> inventory = WithItems(5, "sieve", "basket");

            inventory.Remove("sieve");

            Assert.That(inventory[0], Is.Null);
            Assert.That(inventory.Active, Is.Null);
            Assert.That(inventory[1], Is.EqualTo("basket"));
        }

        [Test]
        public void TryAdd_AfterRemove_ReusesTheFreedSlot()
        {
            Inventory<string> inventory = WithItems(5, "sieve", "basket");
            inventory.Remove("sieve");

            inventory.TryAdd("pick", out int slot);

            Assert.That(slot, Is.Zero);
        }

        [Test]
        public void Select_EmptySlot_MeansEmptyHands()
        {
            Inventory<string> inventory = WithItems(5, "sieve", "basket");

            inventory.Select(3);

            Assert.That(inventory.ActiveSlot, Is.EqualTo(3));
            Assert.That(inventory.Active, Is.Null);
        }

        [Test]
        public void Select_OutOfRange_IsIgnored()
        {
            Inventory<string> inventory = WithItems(5, "sieve");

            inventory.Select(7);
            inventory.Select(-1);

            Assert.That(inventory.ActiveSlot, Is.Zero);
        }

        [Test]
        public void Cycle_SkipsEmptySlotsAndWraps()
        {
            Inventory<string> inventory = WithItems(5, "a", "b");

            inventory.Cycle(1);
            Assert.That(inventory.Active, Is.EqualTo("b"));

            inventory.Cycle(1);
            Assert.That(inventory.Active, Is.EqualTo("a"));

            inventory.Cycle(-1);
            Assert.That(inventory.Active, Is.EqualTo("b"));
        }

        [Test]
        public void Cycle_FromEmptyHands_FindsNextTool()
        {
            Inventory<string> inventory = WithItems(5, "a", "b");
            inventory.Select(4);

            inventory.Cycle(1);

            Assert.That(inventory.Active, Is.EqualTo("a"));
        }

        [Test]
        public void Cycle_WithOnlyOneTool_StaysPut()
        {
            Inventory<string> inventory = WithItems(5, "a");

            inventory.Cycle(1);

            Assert.That(inventory.ActiveSlot, Is.Zero);
        }

        [Test]
        public void HasFreeSlot_TracksAddAndRemove()
        {
            Inventory<string> inventory = WithItems(2, "sieve");
            Assert.That(inventory.HasFreeSlot, Is.True);

            inventory.TryAdd("basket", out _);
            Assert.That(inventory.HasFreeSlot, Is.False);

            inventory.Remove("basket");
            Assert.That(inventory.HasFreeSlot, Is.True);
        }

        [Test]
        public void PlaceAndPickUp_ReturnsToTheFreedSlot()
        {
            Inventory<string> inventory = WithItems(5, "sieve", "basket");
            inventory.Select(1);

            inventory.Remove("basket");
            inventory.TryAdd("basket", out int slot);
            inventory.Select(slot);

            Assert.That(slot, Is.EqualTo(1));
            Assert.That(inventory.Active, Is.EqualTo("basket"));
        }

        [Test]
        public void Changed_FiresOnAddRemoveAndSelect_NotOnNoOps()
        {
            Inventory<string> inventory = new Inventory<string>(3);
            int changes = 0;
            inventory.Changed += () => changes++;

            inventory.TryAdd("a", out _);
            inventory.Select(0);
            inventory.Select(1);
            inventory.Remove("a");
            inventory.Remove("missing");

            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void Restore_SetsSlotsAndActive_FiringOnce()
        {
            Inventory<string> inventory = WithItems(4, "sieve", "basket");
            int changes = 0;
            inventory.Changed += () => changes++;

            inventory.Restore(new[] { null, "basket", null, "sieve" }, 3);

            Assert.That(inventory[0], Is.Null);
            Assert.That(inventory[1], Is.EqualTo("basket"));
            Assert.That(inventory[3], Is.EqualTo("sieve"));
            Assert.That(inventory.Active, Is.EqualTo("sieve"));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Restore_DropsRepeatsAndExtras_AndBadActiveSlot()
        {
            Inventory<string> inventory = new Inventory<string>(2);

            inventory.Restore(new[] { "sieve", "sieve", "basket" }, 9);

            Assert.That(inventory[0], Is.EqualTo("sieve"));
            Assert.That(inventory[1], Is.Null, "A repeat of an item is dropped");
            Assert.That(inventory.IndexOf("basket"), Is.EqualTo(-1), "Beyond the capacity");
            Assert.That(inventory.ActiveSlot, Is.Zero);
        }
    }
}
