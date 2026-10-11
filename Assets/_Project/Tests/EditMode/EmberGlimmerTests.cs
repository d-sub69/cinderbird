using Cinderbird.Embers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class EmberGlimmerTests
    {
        // 16 cells of 0.25 m: ash fills samples 1..15, so every face of the block sits at 0.125 / 3.875 m.
        private const int Cells = 16;
        private const float CellSize = 0.25f;
        private const float TopSurface = 3.875f;
        private const float MaxDepth = 0.35f;
        private const float NoFloor = float.MinValue;

        private static VoxelGrid Block()
        {
            return TerrainTestGrids.SolidBlock(Cells, CellSize);
        }

        [Test]
        public void TryFindSurface_DeepEmber_FindsNothing()
        {
            bool found = EmberGlimmer.TryFindSurface(Block(), new Vector3(2f, 2f, 2f), MaxDepth, NoFloor, out _, out _);

            Assert.That(found, Is.False);
        }

        [Test]
        public void TryFindSurface_EmberJustBelowTop_FindsPointStraightAbove()
        {
            Vector3 ember = new Vector3(2f, 3.7f, 2f);

            bool found = EmberGlimmer.TryFindSurface(Block(), ember, MaxDepth, NoFloor, out Vector3 point, out float depth);

            Assert.That(found, Is.True);
            Assert.That(depth, Is.EqualTo(TopSurface - ember.y).Within(0.01f));
            Assert.That(point.x, Is.EqualTo(ember.x).Within(1e-4f));
            Assert.That(point.y, Is.EqualTo(TopSurface).Within(0.01f));
            Assert.That(point.z, Is.EqualTo(ember.z).Within(1e-4f));
        }

        [Test]
        public void TryFindSurface_PointIsOnTheSurface()
        {
            VoxelGrid grid = Block();

            EmberGlimmer.TryFindSurface(grid, new Vector3(3.6f, 2f, 2f), MaxDepth, NoFloor, out Vector3 point, out _);

            Assert.That(grid.SampleDensity(point), Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void TryFindSurface_PicksTheNearestSurface()
        {
            // 0.1 m below the top, 0.3 m from the +x side.
            Vector3 ember = new Vector3(3.575f, 3.775f, 2f);

            EmberGlimmer.TryFindSurface(Block(), ember, MaxDepth, NoFloor, out Vector3 point, out float depth);

            Assert.That(depth, Is.EqualTo(0.1f).Within(0.01f));
            Assert.That(point.y, Is.GreaterThan(ember.y));
        }

        [Test]
        public void TryFindSurface_OnlySurfaceBelowFloor_IsIgnored()
        {
            // Bottom face is at 0.125 m; everything else is far away.
            Vector3 ember = new Vector3(2f, 0.3f, 2f);

            bool withFloor = EmberGlimmer.TryFindSurface(Block(), ember, MaxDepth, 0.2f, out _, out _);
            bool withoutFloor = EmberGlimmer.TryFindSurface(Block(), ember, MaxDepth, NoFloor, out _, out _);

            Assert.That(withFloor, Is.False);
            Assert.That(withoutFloor, Is.True);
        }

        [Test]
        public void TryFindSurface_AfterDiggingNearby_Appears()
        {
            VoxelGrid grid = Block();
            Vector3 ember = new Vector3(2f, 2f, 2f);
            Assume.That(EmberGlimmer.TryFindSurface(grid, ember, MaxDepth, NoFloor, out _, out _), Is.False);

            // Carve a hole whose wall ends 0.2 m from the ember.
            SphereDigBrush.Dig(grid, new Vector3(2.7f, 2f, 2f), 0.5f);
            bool found = EmberGlimmer.TryFindSurface(grid, ember, MaxDepth, NoFloor, out _, out float depth);

            Assert.That(found, Is.True);
            Assert.That(depth, Is.EqualTo(0.2f).Within(0.03f));
        }
    }
}
