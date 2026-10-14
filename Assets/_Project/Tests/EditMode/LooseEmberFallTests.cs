using Cinderbird.Embers;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class LooseEmberFallTests
    {
        private const float Radius = 0.05f;
        private const float Step = 1f / 60f;
        private static readonly FallParameters Parameters = new FallParameters(9.81f, 0.3f, 1f);

        [Test]
        public void Advance_InTheAir_FallsFasterEachStep()
        {
            FlatFloor floor = new FlatFloor(-100f);

            FallStep first = LooseEmberFall.Advance(Vector3.up, 0f, Radius, Step, Parameters, floor);
            FallStep second = LooseEmberFall.Advance(first.Position, first.Speed, Radius, Step, Parameters, floor);

            Assert.That(first.Position.y, Is.LessThan(1f));
            Assert.That(1f - first.Position.y, Is.LessThan(first.Position.y - second.Position.y));
            Assert.That(second.Resting, Is.False);
        }

        [Test]
        public void Advance_FastLanding_BouncesBackUpWithLessSpeed()
        {
            FlatFloor floor = new FlatFloor(0f);

            FallStep step = LooseEmberFall.Advance(new Vector3(0f, Radius + 0.01f, 0f), 5f, Radius, Step, Parameters, floor);

            Assert.That(step.Position.y, Is.EqualTo(Radius).Within(1e-5f));
            Assert.That(step.Speed, Is.LessThan(0f), "negative = moving up");
            Assert.That(-step.Speed, Is.LessThan(5f));
            Assert.That(step.Resting, Is.False);
        }

        [Test]
        public void Advance_SlowLanding_ComesToRest()
        {
            FlatFloor floor = new FlatFloor(0f);

            FallStep step = LooseEmberFall.Advance(new Vector3(0f, Radius + 0.001f, 0f), 0.2f, Radius, Step, Parameters, floor);

            Assert.That(step.Resting, Is.True);
            Assert.That(step.Speed, Is.Zero);
            Assert.That(step.Position.y, Is.EqualTo(Radius).Within(1e-5f));
        }

        [Test]
        public void Advance_DroppedFromAHeight_EndsRestingOnTheFloorAndNeverBelowIt()
        {
            FlatFloor floor = new FlatFloor(0f);
            Vector3 position = new Vector3(0f, 2f, 0f);
            float speed = 0f;
            bool resting = false;

            for (int i = 0; i < 600 && !resting; i++)
            {
                FallStep step = LooseEmberFall.Advance(position, speed, Radius, Step, Parameters, floor);
                position = step.Position;
                speed = step.Speed;
                resting = step.Resting;
                Assert.That(position.y, Is.GreaterThanOrEqualTo(Radius - 1e-5f));
            }

            Assert.That(resting, Is.True, "settles within 10 seconds");
            Assert.That(position.y, Is.EqualTo(Radius).Within(1e-5f));
        }

        [Test]
        public void Advance_RisingAfterABounce_DoesNotAskForALanding()
        {
            FlatFloor floor = new FlatFloor(0f);

            FallStep step = LooseEmberFall.Advance(new Vector3(0f, 0.5f, 0f), -2f, Radius, Step, Parameters, floor);

            Assert.That(step.Position.y, Is.GreaterThan(0.5f));
            Assert.That(floor.Asked, Is.False);
        }

        private sealed class FlatFloor : IFallSurface
        {
            private readonly float _height;

            public FlatFloor(float height)
            {
                _height = height;
            }

            public bool Asked { get; private set; }

            public bool TryLand(Vector3 center, float drop, float radius, out Vector3 landed)
            {
                Asked = true;
                landed = new Vector3(center.x, _height + radius, center.z);
                return center.y - drop - radius <= _height;
            }
        }
    }
}
