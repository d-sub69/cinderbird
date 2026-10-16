using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class SettleAreaTests
    {
        [Test]
        public void Touches_OverlappingAreas_True()
        {
            SettleArea a = new SettleArea(0, 0, 10, 10);
            SettleArea b = new SettleArea(5, 5, 15, 15);

            Assert.That(a.Touches(b), Is.True);
            Assert.That(b.Touches(a), Is.True);
        }

        [Test]
        public void Touches_NeighborColumns_True()
        {
            // Settling reads a column's neighbors, so areas side by side can't settle at the same time.
            SettleArea a = new SettleArea(0, 0, 10, 10);
            SettleArea b = new SettleArea(11, 0, 20, 10);

            Assert.That(a.Touches(b), Is.True);
        }

        [Test]
        public void Touches_OneColumnApart_False()
        {
            SettleArea a = new SettleArea(0, 0, 10, 10);
            SettleArea b = new SettleArea(12, 0, 20, 10);
            SettleArea c = new SettleArea(0, 12, 10, 20);

            Assert.That(a.Touches(b), Is.False);
            Assert.That(a.Touches(c), Is.False);
            Assert.That(c.Touches(a), Is.False);
        }

        [Test]
        public void Touches_ApartOnOneAxisOnly_False()
        {
            SettleArea a = new SettleArea(0, 0, 10, 10);
            SettleArea b = new SettleArea(5, 30, 8, 40);

            Assert.That(a.Touches(b), Is.False);
        }

        [Test]
        public void AreaOf_CoversDigRadiusPlusReach()
        {
            VoxelWorld world = new VoxelWorld(16, 4, 1, 4, 0.25f, new Vector3(-8f, 0f, -8f));

            SettleArea area = AshSettler.AreaOf(world, Vector3.zero, 0.5f, 1f);

            // Center sample 32, (0.5 + 1) / 0.25 = 6 cells each way.
            Assert.That(area.MinX, Is.EqualTo(26));
            Assert.That(area.MaxX, Is.EqualTo(38));
            Assert.That(area.MinZ, Is.EqualTo(26));
            Assert.That(area.MaxZ, Is.EqualTo(38));
        }

        [Test]
        public void AreaOf_ClampsInsideTheBorder()
        {
            VoxelWorld world = new VoxelWorld(16, 4, 1, 4, 0.25f, new Vector3(-8f, 0f, -8f));

            SettleArea area = AshSettler.AreaOf(world, new Vector3(-8f, 0f, 8f), 1f, 1f);

            Assert.That(area.MinX, Is.EqualTo(1));
            Assert.That(area.MaxZ, Is.EqualTo(world.SamplesZ - 2));
        }

        [Test]
        public void AreaOf_FarApartDigs_DoNotTouch()
        {
            VoxelWorld world = new VoxelWorld(16, 4, 1, 4, 0.25f, new Vector3(-8f, 0f, -8f));

            SettleArea left = AshSettler.AreaOf(world, new Vector3(-5f, 0f, 0f), 0.5f, 1f);
            SettleArea right = AshSettler.AreaOf(world, new Vector3(5f, 0f, 0f), 0.5f, 1f);

            Assert.That(left.Touches(right), Is.False);
        }
    }
}
