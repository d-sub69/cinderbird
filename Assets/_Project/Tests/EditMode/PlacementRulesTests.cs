using Cinderbird.Player;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class PlacementRulesTests
    {
        private const float MaxSlope = 30f;

        private static Vector3 NormalTiltedBy(float degrees)
        {
            return Quaternion.Euler(degrees, 0f, 0f) * Vector3.up;
        }

        [Test]
        public void FlatSolidGround_IsValid()
        {
            Assert.That(PlacementRules.Evaluate(true, Vector3.up, MaxSlope, false, false), Is.EqualTo(PlacementResult.Valid));
        }

        [Test]
        public void NoSurface_IsReported()
        {
            Assert.That(PlacementRules.Evaluate(false, Vector3.up, MaxSlope, false, false), Is.EqualTo(PlacementResult.NoSurface));
        }

        [Test]
        public void AshPile_IsNeverValid_EvenWhenFlat()
        {
            Assert.That(PlacementRules.Evaluate(true, Vector3.up, MaxSlope, true, false), Is.EqualTo(PlacementResult.OnAsh));
        }

        [Test]
        public void AshPile_TakesPriorityOverSlope()
        {
            Assert.That(PlacementRules.Evaluate(true, NormalTiltedBy(60f), MaxSlope, true, false), Is.EqualTo(PlacementResult.OnAsh));
        }

        [Test]
        public void Slope_JustUnderLimit_IsValid_JustOver_IsTooSteep()
        {
            Assert.That(PlacementRules.Evaluate(true, NormalTiltedBy(29f), MaxSlope, false, false), Is.EqualTo(PlacementResult.Valid));
            Assert.That(PlacementRules.Evaluate(true, NormalTiltedBy(31f), MaxSlope, false, false), Is.EqualTo(PlacementResult.TooSteep));
        }

        [Test]
        public void BlockedSpace_IsReported()
        {
            Assert.That(PlacementRules.Evaluate(true, Vector3.up, MaxSlope, false, true), Is.EqualTo(PlacementResult.Blocked));
        }

        [Test]
        public void Wall_IsTooSteep()
        {
            Assert.That(PlacementRules.Evaluate(true, Vector3.forward, MaxSlope, false, false), Is.EqualTo(PlacementResult.TooSteep));
        }
    }
}
