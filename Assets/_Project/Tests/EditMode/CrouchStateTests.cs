using Cinderbird.Player;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class CrouchStateTests
    {
        private const float Stand = 2f;
        private const float Crouch = 1f;
        private const float Speed = 5f;
        private const float Dt = 0.01f;

        private static CrouchState Create()
        {
            return new CrouchState(Stand, Crouch, Speed);
        }

        private static void Run(CrouchState state, bool wantsCrouch, bool canStand, float seconds)
        {
            int frames = (int)(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                state.Tick(wantsCrouch, canStand, Dt);
            }
        }

        [Test]
        public void New_IsStanding()
        {
            CrouchState state = Create();

            Assert.That(state.Height, Is.EqualTo(Stand));
            Assert.That(state.IsCrouching, Is.False);
        }

        [Test]
        public void HoldingCrouch_LowersAtTransitionSpeed()
        {
            CrouchState state = Create();

            state.Tick(true, true, 0.1f);

            Assert.That(state.Height, Is.EqualTo(Stand - Speed * 0.1f).Within(1e-5f));
            Assert.That(state.IsCrouching, Is.True);
        }

        [Test]
        public void HoldingCrouch_StopsAtCrouchHeight()
        {
            CrouchState state = Create();

            Run(state, true, true, 2f);

            Assert.That(state.Height, Is.EqualTo(Crouch));
        }

        [Test]
        public void Releasing_WithHeadroom_StandsBackUp()
        {
            CrouchState state = Create();
            Run(state, true, true, 2f);

            Run(state, false, true, 2f);

            Assert.That(state.Height, Is.EqualTo(Stand));
            Assert.That(state.IsCrouching, Is.False);
        }

        [Test]
        public void Releasing_WithoutHeadroom_HoldsHeight()
        {
            CrouchState state = Create();
            Run(state, true, true, 2f);

            Run(state, false, false, 2f);

            Assert.That(state.Height, Is.EqualTo(Crouch));
            Assert.That(state.IsCrouching, Is.True);
        }

        [Test]
        public void CrouchHeightAboveStand_IsClampedToStand()
        {
            CrouchState state = new CrouchState(Stand, Stand + 1f, Speed);

            Run(state, true, true, 2f);

            Assert.That(state.Height, Is.EqualTo(Stand));
        }

        [Test]
        public void SnapTo_JumpsWithoutTransition()
        {
            CrouchState state = Create();

            state.SnapTo(true);
            Assert.That(state.Height, Is.EqualTo(Crouch));
            Assert.That(state.IsCrouching, Is.True);

            state.SnapTo(false);
            Assert.That(state.Height, Is.EqualTo(Stand));
        }
    }
}
