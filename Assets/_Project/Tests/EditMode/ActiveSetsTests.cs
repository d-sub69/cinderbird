using Cinderbird.Offerings;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class ActiveSetsTests
    {
        [Test]
        public void CompletedSet_TakesAFreeSlotByItself()
        {
            ActiveSets active = new ActiveSets(1);

            active.Update(new[] { true, false }, 0);

            Assert.That(active.IsActive(0), Is.True);
        }

        [Test]
        public void SecondCompletedSet_WaitsWhenEverySlotIsTaken_UntilChosen()
        {
            ActiveSets active = new ActiveSets(1);
            bool[] complete = { true, true };
            active.Update(complete, 0);

            active.Update(complete, 1);
            Assert.That(active.IsActive(1), Is.False, "one slot, already taken");

            Assert.That(active.Activate(1, complete), Is.True);
            Assert.That(active.IsActive(1), Is.True);
            Assert.That(active.IsActive(0), Is.False, "the player swapped them");
        }

        [Test]
        public void SetLosingAPiece_StopsBeingActive()
        {
            ActiveSets active = new ActiveSets(1);
            active.Update(new[] { true }, 0);

            active.Update(new[] { false }, -1);

            Assert.That(active.Active, Is.Empty);
        }

        [Test]
        public void Activate_AnIncompleteSet_IsRefused()
        {
            ActiveSets active = new ActiveSets(1);

            Assert.That(active.Activate(0, new[] { false }), Is.False);
            Assert.That(active.Active, Is.Empty);
        }

        [Test]
        public void MoreSlots_LetSeveralSetsGiveTheirBonus()
        {
            ActiveSets active = new ActiveSets(2);
            bool[] complete = { true, true };

            active.Update(complete, 0);
            active.Update(complete, 1);

            Assert.That(active.Active, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void Restore_KeepsOnlyCompleteSets_AndNoMoreThanTheSlots()
        {
            ActiveSets active = new ActiveSets(1);

            active.Restore(new[] { 2, 0, 1 }, new[] { true, true, false });

            Assert.That(active.Active, Is.EqualTo(new[] { 0 }), "2 isn't complete; one slot");
        }
    }
}
