using Cinderbird.Nest;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class FireFlareTests
    {
        private const float HalfLife = 0.4f;
        private const float MaxFlare = 1.5f;

        [Test]
        public void Flare_AfterOneHalfLife_IsHalved()
        {
            FireFlare fire = new FireFlare(HalfLife, MaxFlare);
            fire.AddFlare(1f);

            fire.Tick(HalfLife);

            Assert.That(fire.Flare, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Flare_DecayIsFrameRateIndependent()
        {
            FireFlare oneStep = new FireFlare(HalfLife, MaxFlare);
            FireFlare manySteps = new FireFlare(HalfLife, MaxFlare);
            oneStep.AddFlare(1f);
            manySteps.AddFlare(1f);

            oneStep.Tick(0.3f);
            for (int i = 0; i < 30; i++)
            {
                manySteps.Tick(0.01f);
            }

            Assert.That(manySteps.Flare, Is.EqualTo(oneStep.Flare).Within(1e-4f));
        }

        [Test]
        public void Flare_IsCappedAtMax()
        {
            FireFlare fire = new FireFlare(HalfLife, MaxFlare);

            fire.AddFlare(1f);
            fire.AddFlare(1f);

            Assert.That(fire.Flare, Is.EqualTo(MaxFlare));
        }

        [Test]
        public void Flare_EventuallyReachesExactlyZero()
        {
            FireFlare fire = new FireFlare(HalfLife, MaxFlare);
            fire.AddFlare(1f);

            for (int i = 0; i < 1000; i++)
            {
                fire.Tick(0.05f);
            }

            Assert.That(fire.Flare, Is.Zero);
        }

        [Test]
        public void AddFlare_NegativeOrZero_IsIgnored()
        {
            FireFlare fire = new FireFlare(HalfLife, MaxFlare);
            fire.AddFlare(0.5f);

            fire.AddFlare(-1f);
            fire.AddFlare(0f);

            Assert.That(fire.Flare, Is.EqualTo(0.5f));
        }

        [Test]
        public void SetProgress_ClampsToZeroToOne()
        {
            FireFlare fire = new FireFlare(HalfLife, MaxFlare);

            fire.SetProgress(2f);
            Assert.That(fire.BaseLevel, Is.EqualTo(1f));

            fire.SetProgress(-1f);
            Assert.That(fire.BaseLevel, Is.Zero);
        }
    }
}
