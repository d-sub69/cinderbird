using Cinderbird.Player;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class LookRotationTests
    {
        private const float MaxPitch = 85f;

        [Test]
        public void Apply_MouseUp_PitchesUp()
        {
            LookRotation look = new LookRotation(MaxPitch);

            look.Apply(new Vector2(0f, 10f), 1f);

            Assert.That(look.Pitch, Is.EqualTo(-10f).Within(1e-4f));
        }

        [Test]
        public void Apply_LargeVerticalDelta_ClampsPitch()
        {
            LookRotation look = new LookRotation(MaxPitch);

            look.Apply(new Vector2(0f, 10000f), 1f);
            Assert.That(look.Pitch, Is.EqualTo(-MaxPitch));

            look.Apply(new Vector2(0f, -20000f), 1f);
            Assert.That(look.Pitch, Is.EqualTo(MaxPitch));
        }

        [Test]
        public void Apply_YawPastFullTurn_WrapsInto0To360()
        {
            LookRotation look = new LookRotation(MaxPitch, 350f);

            look.Apply(new Vector2(20f, 0f), 1f);
            Assert.That(look.Yaw, Is.EqualTo(10f).Within(1e-4f));

            look.Apply(new Vector2(-30f, 0f), 1f);
            Assert.That(look.Yaw, Is.EqualTo(340f).Within(1e-4f));
        }

        [Test]
        public void Apply_ScalesBySensitivity()
        {
            LookRotation look = new LookRotation(MaxPitch);

            look.Apply(new Vector2(10f, 0f), 0.5f);

            Assert.That(look.Yaw, Is.EqualTo(5f).Within(1e-4f));
        }
    }
}
