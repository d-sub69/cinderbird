using Cinderbird.Sifting;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class SieveShakerTests
    {
        private const float Dt = 1f / 60f;
        private const float MaxRate = 0.12f;          // 120 L/s
        private const float HoldEnergy = 0.35f;
        private const float FullWiggle = 1500f;
        private const float RiseTime = 0.25f;
        private const float FallTime = 0.4f;
        private const float TypicalScoop = 0.18f;     // 180 L

        private static SieveShaker CreateShaker()
        {
            return new SieveShaker(new ShakeParameters(MaxRate, HoldEnergy, FullWiggle, RiseTime, FallTime, 0.1f));
        }

        private static void Run(SieveShaker shaker, bool held, float wiggle, float seconds)
        {
            int frames = (int)(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                shaker.Tick(held, wiggle, Dt);
            }
        }

        private static float SecondsToShakeOut(SieveShaker shaker, float volume, float wiggle)
        {
            float remaining = volume;
            float time = 0f;
            while (remaining > 0f && time < 30f)
            {
                shaker.Tick(true, wiggle, Dt);
                remaining -= shaker.AshToRemove(Dt);
                time += Dt;
            }

            return time;
        }

        [Test]
        public void NotHeld_StaysAtZero_AndRemovesNothing()
        {
            SieveShaker shaker = CreateShaker();

            Run(shaker, false, 5000f, 1f);

            Assert.That(shaker.Energy, Is.Zero);
            Assert.That(shaker.AshToRemove(Dt), Is.Zero);
        }

        [Test]
        public void HeldStill_SettlesAtHoldEnergy()
        {
            SieveShaker shaker = CreateShaker();

            Run(shaker, true, 0f, 2f);

            Assert.That(shaker.Energy, Is.EqualTo(HoldEnergy).Within(1e-4f));
        }

        [Test]
        public void HeldWithFullWiggle_ReachesFullEnergy()
        {
            SieveShaker shaker = CreateShaker();

            Run(shaker, true, FullWiggle, 2f);

            Assert.That(shaker.Energy, Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void HugeWiggle_NeverExceedsFullEnergy()
        {
            SieveShaker shaker = CreateShaker();

            Run(shaker, true, FullWiggle * 100f, 2f);

            Assert.That(shaker.Energy, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void Energy_RisesNoFasterThanRiseTime()
        {
            SieveShaker shaker = CreateShaker();

            shaker.Tick(true, FullWiggle * 100f, 0.05f);

            Assert.That(shaker.Energy, Is.LessThanOrEqualTo(0.05f / RiseTime + 1e-5f));
        }

        [Test]
        public void Released_DecaysToZeroWithinFallTime()
        {
            SieveShaker shaker = CreateShaker();
            Run(shaker, true, FullWiggle, 2f);

            Run(shaker, false, 0f, FallTime + 0.05f);

            Assert.That(shaker.Energy, Is.Zero);
        }

        [Test]
        public void AshToRemove_IsEnergyTimesRateTimesTime()
        {
            SieveShaker shaker = CreateShaker();
            Run(shaker, true, 0f, 2f);

            Assert.That(shaker.AshToRemove(0.5f), Is.EqualTo(HoldEnergy * MaxRate * 0.5f).Within(1e-6f));
        }

        [Test]
        public void TypicalScoop_VigorousShaking_TakesAboutOneAndAHalfSeconds()
        {
            float seconds = SecondsToShakeOut(CreateShaker(), TypicalScoop, FullWiggle);

            Assert.That(seconds, Is.InRange(1.3f, 2.0f));
        }

        [Test]
        public void TypicalScoop_HoldingStill_TakesAboutFourSeconds()
        {
            float seconds = SecondsToShakeOut(CreateShaker(), TypicalScoop, 0f);

            Assert.That(seconds, Is.InRange(3.8f, 5.0f));
        }

        [Test]
        public void ZeroDeltaTime_ChangesNothing()
        {
            SieveShaker shaker = CreateShaker();

            shaker.Tick(true, FullWiggle, 0f);

            Assert.That(shaker.Energy, Is.Zero);
        }
    }
}
