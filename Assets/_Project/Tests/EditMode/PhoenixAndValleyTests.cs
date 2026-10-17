using System.Collections.Generic;
using Cinderbird.Core;
using Cinderbird.Nest;
using Cinderbird.Valley;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class PhoenixAndValleyTests
    {
        private static readonly PhoenixStage[] Stages =
        {
            new PhoenixStage(1, 0.6f, 40f, 0.35f, Color.red, 0.8f, 0f, 0.04f, 0f, "one"),
            new PhoenixStage(2, 0.85f, 110f, 0.65f, Color.yellow, 1f, 0.35f, 0.06f, 0.05f, "two"),
            new PhoenixStage(3, 1f, 220f, 1f, Color.white, 1.35f, 0.8f, 0.05f, 0.12f, "three"),
        };

        [Test]
        public void Phoenix_StageFollowsTheHeatLevel()
        {
            Assert.That(PhoenixLook.StageFor(Stages, 0), Is.EqualTo(0));
            Assert.That(PhoenixLook.StageFor(Stages, 1), Is.EqualTo(1));
            Assert.That(PhoenixLook.StageFor(Stages, 3), Is.EqualTo(3));
            Assert.That(PhoenixLook.StageFor(Stages, 7), Is.EqualTo(3), "past the last stage it stays there");
            Assert.That(PhoenixLook.Of(Stages, 0).IsVisible, Is.False);
            Assert.That(PhoenixLook.Of(Stages, 2).Scale, Is.EqualTo(0.85f));
        }

        [Test]
        public void Phoenix_StagesBlend()
        {
            PhoenixLook half = PhoenixLook.Lerp(PhoenixLook.Of(Stages, 1), PhoenixLook.Of(Stages, 2), 0.5f);

            Assert.That(half.Scale, Is.EqualTo(0.725f).Within(1e-4f));
            Assert.That(half.ParticlesPerSecond, Is.EqualTo(75f).Within(1e-4f));
            Assert.That(half.Lift, Is.EqualTo(0.175f).Within(1e-4f));
            Assert.That(half.Color.g, Is.EqualTo(Color.yellow.g * 0.5f).Within(1e-4f), "halfway from red to yellow");
        }

        [Test]
        public void Phoenix_DefaultStagesAreClearlyApart()
        {
            PhoenixStage[] stages = PhoenixSettings.DefaultStages();

            for (int i = 1; i < stages.Length; i++)
            {
                PhoenixLook before = stages[i - 1].Look;
                PhoenixLook after = stages[i].Look;
                Assert.That(after.Scale, Is.GreaterThan(before.Scale * 1.3f), "noticeably bigger");
                Assert.That(after.ParticlesPerSecond, Is.GreaterThan(before.ParticlesPerSecond * 2f), "much fuller");
                Assert.That(after.Lift, Is.GreaterThan(before.Lift), "higher above the fire");
                Assert.That(after.Color, Is.Not.EqualTo(before.Color), "another color");
            }
        }

        [Test]
        public void Phoenix_ShapeIsAClosedFanWithBothWings()
        {
            MeshData shape = PhoenixShape.Build();

            Assert.That(shape.Triangles.Length % 3, Is.EqualTo(0));
            foreach (int index in shape.Triangles)
            {
                Assert.That(index, Is.InRange(0, shape.Vertices.Length - 1));
            }

            float left = float.MaxValue;
            float right = float.MinValue;
            foreach (Vector3 vertex in shape.Vertices)
            {
                left = Mathf.Min(left, vertex.x);
                right = Mathf.Max(right, vertex.x);
            }

            Assert.That(right, Is.EqualTo(-left).Within(1e-4f), "the wings mirror each other");
            Assert.That(right - left, Is.GreaterThan(1.5f));
        }

        private static readonly ValleyThawStage[] Thaw =
        {
            new ValleyThawStage(2, new Vector3(0f, 0f, -100f), 40f, new Vector2(0.3f, 0.5f), new[] { 0 }, "first"),
            new ValleyThawStage(3, new Vector3(100f, 0f, -150f), 30f, new Vector2(0.6f, 0.8f), new[] { 1, 2 }, "second"),
        };

        [Test]
        public void Valley_StageFollowsTheHeatLevel()
        {
            Assert.That(ValleyThawing.StageFor(Thaw, 1), Is.EqualTo(0));
            Assert.That(ValleyThawing.StageFor(Thaw, 2), Is.EqualTo(1));
            Assert.That(ValleyThawing.StageFor(Thaw, 5), Is.EqualTo(2));
        }

        [Test]
        public void Valley_PatchThaws_SpreadingFromItsMiddle()
        {
            Vector3 middle = new Vector3(0f, 0f, -100f);
            Vector3 nearRim = new Vector3(35f, 0f, -100f);
            Vector3 outside = new Vector3(60f, 0f, -100f);

            Assert.That(ValleyThawing.GroundAt(Thaw, 0, 1f, middle), Is.EqualTo(0f), "before the stage: snow");
            Assert.That(ValleyThawing.GroundAt(Thaw, 1, 1f, middle), Is.EqualTo(1f));
            Assert.That(ValleyThawing.GroundAt(Thaw, 1, 1f, outside), Is.EqualTo(0f));
            Assert.That(ValleyThawing.GroundAt(Thaw, 1, 0.5f, nearRim), Is.EqualTo(0f), "halfway grown, the rim is still snow");
            Assert.That(ValleyThawing.GroundAt(Thaw, 2, 0f, middle), Is.EqualTo(1f), "an earlier stage stays thawed");
        }

        [Test]
        public void Valley_RiverThawsItsStretch_AndWindowsLight()
        {
            Assert.That(ValleyThawing.RiverAt(Thaw, 1, 1f, 0.4f), Is.EqualTo(1f));
            Assert.That(ValleyThawing.RiverAt(Thaw, 1, 1f, 0.7f), Is.EqualTo(0f), "the next stage's stretch is still ice");
            Assert.That(ValleyThawing.RiverAt(Thaw, 1, 0.5f, 0.32f), Is.EqualTo(0f), "growing from the stretch's middle");
            Assert.That(ValleyThawing.WindowAt(Thaw, 1, 0.5f, 0), Is.EqualTo(0.5f));
            Assert.That(ValleyThawing.WindowAt(Thaw, 2, 0f, 0), Is.EqualTo(1f), "lit for good");
            Assert.That(ValleyThawing.WindowAt(Thaw, 1, 1f, 2), Is.EqualTo(0f));
        }

        private static ValleyShape Shape()
        {
            Vector3[] river = { new Vector3(-100f, 0f, -50f), new Vector3(100f, 0f, -50f) };
            return new ValleyShape(new Vector3(0f, -35f, -100f), new Vector2(200f, 200f), 20, 10f, 40f, 0.02f, 3, river, 8f, 2f,
                new Vector3(0f, 0f, -120f), 20f);
        }

        [Test]
        public void Valley_RiverDistanceAndHowFarAlong()
        {
            float distance = ValleyBuilder.DistanceToRiver(Shape(), 50f, -60f, out float along);

            Assert.That(distance, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(along, Is.EqualTo(0.75f).Within(1e-3f));
        }

        [Test]
        public void Valley_FlatAtTheVillage_BedAtTheRiver()
        {
            ValleyShape shape = Shape();

            Assert.That(ValleyBuilder.HeightAt(shape, 0f, -120f), Is.EqualTo(-35f).Within(0.01f), "flattened for the houses");
            Assert.That(ValleyBuilder.HeightAt(shape, 0f, -50f), Is.LessThan(-36.5f), "the riverbed is lower");
        }

        [Test]
        public void Valley_GroundIsFlatShaded_CliffFacesTheValley()
        {
            ValleyShape shape = Shape();
            MeshData ground = ValleyBuilder.BuildGround(shape, Color.white);
            MeshData cliff = ValleyBuilder.BuildCliff(-10f, 10f, 2f, 0f, -20f, 1, Color.grey);

            Assert.That(ground.Vertices.Length, Is.EqualTo(20 * 20 * 6), "three vertices of its own per triangle");
            Vector3 a = cliff.Vertices[0];
            Vector3 normal = Vector3.Cross(cliff.Vertices[1] - a, cliff.Vertices[2] - a);
            Assert.That(normal.z, Is.LessThan(0f), "toward the valley");
        }

        [Test]
        public void Valley_HousesStandOnTheGroundAroundTheVillage()
        {
            ValleyShape shape = Shape();
            List<Pose> spots = new List<Pose>();

            ValleyBuilder.HouseSpots(shape, 6, spots);

            Assert.That(spots.Count, Is.EqualTo(6));
            foreach (Pose spot in spots)
            {
                Assert.That(Vector2.Distance(new Vector2(spot.position.x, spot.position.z), new Vector2(0f, -120f)), Is.LessThanOrEqualTo(20f));
                Assert.That(spot.position.y, Is.EqualTo(ValleyBuilder.HeightAt(shape, spot.position.x, spot.position.z)).Within(1e-4f));
            }
        }
    }
}
