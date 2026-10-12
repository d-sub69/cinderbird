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

        private static PlacementResult Evaluate(
            bool hasSurface = true,
            Vector3? normal = null,
            bool onAsh = false,
            bool inZone = false,
            bool blocked = false)
        {
            return PlacementRules.Evaluate(hasSurface, normal ?? Vector3.up, MaxSlope, onAsh, inZone, blocked);
        }

        [Test]
        public void FlatSolidGround_IsValid()
        {
            Assert.That(Evaluate(), Is.EqualTo(PlacementResult.Valid));
        }

        [Test]
        public void NoSurface_IsReported()
        {
            Assert.That(Evaluate(hasSurface: false), Is.EqualTo(PlacementResult.NoSurface));
        }

        [Test]
        public void AshPile_IsNeverValid_EvenWhenFlat()
        {
            Assert.That(Evaluate(onAsh: true), Is.EqualTo(PlacementResult.OnAsh));
        }

        [Test]
        public void AshPile_TakesPriorityOverSlope()
        {
            Assert.That(Evaluate(normal: NormalTiltedBy(60f), onAsh: true), Is.EqualTo(PlacementResult.OnAsh));
        }

        [Test]
        public void Slope_JustUnderLimit_IsValid_JustOver_IsTooSteep()
        {
            Assert.That(Evaluate(normal: NormalTiltedBy(29f)), Is.EqualTo(PlacementResult.Valid));
            Assert.That(Evaluate(normal: NormalTiltedBy(31f)), Is.EqualTo(PlacementResult.TooSteep));
        }

        [Test]
        public void BlockedSpace_IsReported()
        {
            Assert.That(Evaluate(blocked: true), Is.EqualTo(PlacementResult.Blocked));
        }

        [Test]
        public void Wall_IsTooSteep()
        {
            Assert.That(Evaluate(normal: Vector3.forward), Is.EqualTo(PlacementResult.TooSteep));
        }

        [Test]
        public void NoPlacementZone_IsRefused_EvenOnFlatGround()
        {
            Assert.That(Evaluate(inZone: true), Is.EqualTo(PlacementResult.InNoPlacementZone));
        }

        [Test]
        public void NoPlacementZone_TakesPriorityOverSlopeAndRoom()
        {
            Assert.That(Evaluate(normal: NormalTiltedBy(60f), inZone: true, blocked: true), Is.EqualTo(PlacementResult.InNoPlacementZone));
        }
    }
}
