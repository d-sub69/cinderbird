using Cinderbird.Embers;
using Cinderbird.Sifting;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class SieveGlimmerTests
    {
        private const float EmberTop = 0.02f;
        private const float MaxDepth = 0.06f;
        private const float Deep = 0.2f;

        [Test]
        public void Brightness_Uncovered_IsZero()
        {
            Assert.That(SieveGlimmer.Brightness(0.01f, EmberTop, MaxDepth, Deep), Is.Zero);
        }

        [Test]
        public void Brightness_SurfaceExactlyAtEmberTop_IsZero()
        {
            Assert.That(SieveGlimmer.Brightness(EmberTop, EmberTop, MaxDepth, Deep), Is.Zero);
        }

        [Test]
        public void Brightness_ThinnerAsh_IsBrighter()
        {
            float thick = SieveGlimmer.Brightness(0.06f, EmberTop, MaxDepth, Deep);
            float thin = SieveGlimmer.Brightness(0.03f, EmberTop, MaxDepth, Deep);

            Assert.That(thin, Is.GreaterThan(thick));
        }

        [Test]
        public void Brightness_AtOrBeyondMaxDepth_IsDeepBrightness()
        {
            Assert.That(SieveGlimmer.Brightness(EmberTop + MaxDepth, EmberTop, MaxDepth, Deep), Is.EqualTo(Deep).Within(1e-5f));
            Assert.That(SieveGlimmer.Brightness(EmberTop + MaxDepth * 3f, EmberTop, MaxDepth, Deep), Is.EqualTo(Deep).Within(1e-5f));
        }

        [Test]
        public void Brightness_NeverExceedsOne()
        {
            Assert.That(SieveGlimmer.Brightness(EmberTop + 0.0001f, EmberTop, MaxDepth, Deep), Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void Twinkle_StaysBetweenOneMinusAmountAndOne()
        {
            for (int i = 0; i < 100; i++)
            {
                float dim = GlimmerTwinkle.Dim(i * 0.037f, GlimmerTwinkle.PhaseFor(i), 1.5f, 0.5f);
                Assert.That(dim, Is.InRange(0.5f, 1f));
            }
        }

        [Test]
        public void Twinkle_ZeroSpeedOrAmount_IsSteady()
        {
            Assert.That(GlimmerTwinkle.Dim(1.234f, 0f, 0f, 0.5f), Is.EqualTo(1f));
            Assert.That(GlimmerTwinkle.Dim(1.234f, 0f, 1.5f, 0f), Is.EqualTo(1f));
        }
    }
}
