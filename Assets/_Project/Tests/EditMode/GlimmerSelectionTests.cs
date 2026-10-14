using System.Collections.Generic;
using Cinderbird.Embers;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class GlimmerSelectionTests
    {
        private const float FollowDistance = 1f;

        private static GlimmerPoint At(float x, int tier = 0)
        {
            return new GlimmerPoint(new Vector3(x, 0f, 0f), 0.1f, tier, new Vector3(x, -0.1f, 0f));
        }

        private static List<GlimmerPoint>[] Chunks(params GlimmerPoint[][] perChunk)
        {
            List<GlimmerPoint>[] chunks = new List<GlimmerPoint>[perChunk.Length];
            for (int i = 0; i < perChunk.Length; i++)
            {
                chunks[i] = new List<GlimmerPoint>(perChunk[i]);
            }

            return chunks;
        }

        [Test]
        public void Pick_KeepsTheNearestAcrossChunks_NearestFirst()
        {
            GlimmerSelection selection = new GlimmerSelection(2);

            selection.Pick(Chunks(new[] { At(5f), At(1f) }, new[] { At(3f), At(2f) }), Vector3.zero, FollowDistance);

            Assert.That(selection.Count, Is.EqualTo(2));
            Assert.That(selection[0].SurfacePosition.x, Is.EqualTo(1f));
            Assert.That(selection[1].SurfacePosition.x, Is.EqualTo(2f));
        }

        [Test]
        public void Pick_FewerCandidatesThanSlots_PicksThemAll()
        {
            GlimmerSelection selection = new GlimmerSelection(5);

            selection.Pick(Chunks(new[] { At(1f) }), Vector3.zero, FollowDistance);

            Assert.That(selection.Count, Is.EqualTo(1));
        }

        [Test]
        public void Pick_NewGlimmerFarFromAnyOld_AppearsInPlace()
        {
            GlimmerSelection selection = new GlimmerSelection(1);
            selection.Pick(Chunks(new[] { At(0f) }), Vector3.zero, FollowDistance);

            selection.Pick(Chunks(new[] { At(5f) }), Vector3.zero, FollowDistance);

            Assert.That(selection.ShownAt(0).x, Is.EqualTo(5f));
        }

        [Test]
        public void Pick_SameTierGlimmerMovedALittle_StartsWhereTheOldOneWasAndGlides()
        {
            GlimmerSelection selection = new GlimmerSelection(1);
            selection.Pick(Chunks(new[] { At(0f) }), Vector3.zero, FollowDistance);

            selection.Pick(Chunks(new[] { At(0.5f) }), Vector3.zero, FollowDistance);

            Assert.That(selection.ShownAt(0).x, Is.EqualTo(0f));
            Assert.That(selection.Glide(0, 0.5f), Is.True);
            Assert.That(selection.ShownAt(0).x, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void Pick_OtherTierCloseBy_IsNotTakenForTheSameEmber()
        {
            GlimmerSelection selection = new GlimmerSelection(1);
            selection.Pick(Chunks(new[] { At(0f, tier: 0) }), Vector3.zero, FollowDistance);

            selection.Pick(Chunks(new[] { At(0.5f, tier: 1) }), Vector3.zero, FollowDistance);

            Assert.That(selection.ShownAt(0).x, Is.EqualTo(0.5f));
        }

        [Test]
        public void Pick_TwoNewGlimmers_DontBothClaimTheSameOldOne()
        {
            GlimmerSelection selection = new GlimmerSelection(2);
            selection.Pick(Chunks(new[] { At(0f) }), Vector3.zero, FollowDistance);

            selection.Pick(Chunks(new[] { At(0.2f), At(0.4f) }), Vector3.zero, FollowDistance);

            bool firstFromOld = Mathf.Approximately(selection.ShownAt(0).x, 0f);
            bool secondFromOld = Mathf.Approximately(selection.ShownAt(1).x, 0f);
            Assert.That(firstFromOld ^ secondFromOld, Is.True);
        }

        [Test]
        public void Glide_AtTarget_ReportsNoChange()
        {
            GlimmerSelection selection = new GlimmerSelection(1);
            selection.Pick(Chunks(new[] { At(1f) }), Vector3.zero, FollowDistance);

            Assert.That(selection.Glide(0, 0.5f), Is.False);
        }
    }
}
