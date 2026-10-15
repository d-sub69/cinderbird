using System.Collections.Generic;
using Cinderbird.Core;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class PauseStateTests
    {
        [Test]
        public void New_IsLoading_AndFrozen()
        {
            PauseState state = new PauseState();

            Assert.That(state.Phase, Is.EqualTo(GamePhase.Loading));
            Assert.That(state.IsFrozen, Is.True);
        }

        [Test]
        public void WhileLoading_CantPauseOrResume()
        {
            PauseState state = new PauseState();

            Assert.That(state.Pause(), Is.False);
            Assert.That(state.Resume(), Is.False);
            Assert.That(state.Phase, Is.EqualTo(GamePhase.Loading));
        }

        [Test]
        public void FinishLoading_StartsPlay_OnlyOnce()
        {
            PauseState state = new PauseState();

            Assert.That(state.FinishLoading(), Is.True);
            Assert.That(state.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(state.IsFrozen, Is.False);
            Assert.That(state.FinishLoading(), Is.False);
        }

        [Test]
        public void PauseAndResume_Alternate()
        {
            PauseState state = new PauseState();
            state.FinishLoading();

            Assert.That(state.Resume(), Is.False, "Not paused yet");
            Assert.That(state.Pause(), Is.True);
            Assert.That(state.IsFrozen, Is.True);
            Assert.That(state.Pause(), Is.False, "Already paused");
            Assert.That(state.Resume(), Is.True);
            Assert.That(state.Phase, Is.EqualTo(GamePhase.Playing));
        }

        [Test]
        public void Menu_KeepsTheGameRunning_AndOnlyOpensWhilePlaying()
        {
            PauseState state = new PauseState();
            Assert.That(state.OpenMenu(), Is.False, "Not while loading");
            state.FinishLoading();

            Assert.That(state.OpenMenu(), Is.True);
            Assert.That(state.Phase, Is.EqualTo(GamePhase.InMenu));
            Assert.That(state.IsFrozen, Is.False, "The shop doesn't stop the world");
            Assert.That(state.OpenMenu(), Is.False, "Already open");
            Assert.That(state.CloseMenu(), Is.True);
            Assert.That(state.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(state.CloseMenu(), Is.False);
        }

        [Test]
        public void Pause_FromAMenu_ClosesIt_AndResumeGoesBackToPlay()
        {
            PauseState state = new PauseState();
            state.FinishLoading();
            state.OpenMenu();

            Assert.That(state.Pause(), Is.True);
            Assert.That(state.IsFrozen, Is.True);
            Assert.That(state.OpenMenu(), Is.False, "No menu while paused");
            Assert.That(state.Resume(), Is.True);
            Assert.That(state.Phase, Is.EqualTo(GamePhase.Playing));
        }

        [Test]
        public void Changed_FiresOnlyForRealChanges()
        {
            PauseState state = new PauseState();
            List<GamePhase> seen = new List<GamePhase>();
            state.Changed += seen.Add;

            state.Pause();
            state.FinishLoading();
            state.Pause();
            state.Pause();
            state.Resume();

            Assert.That(seen, Is.EqualTo(new[] { GamePhase.Playing, GamePhase.Paused, GamePhase.Playing }));
        }
    }
}
