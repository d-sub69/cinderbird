using Cinderbird.Embers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class LayerEmberCountsTests
    {
        private static Ember InLayer(byte layer)
        {
            return new Ember(Vector3.zero, 0, layer);
        }

        [Test]
        public void ClearedShare_FollowsWhatIsTakenAndReturned()
        {
            LayerEmberCounts counts = new LayerEmberCounts(2);
            for (int i = 0; i < 4; i++)
            {
                counts.Returned(InLayer(0));
            }

            counts.SetInitialToCurrent();
            counts.Taken(InLayer(0));
            counts.Taken(InLayer(0));
            counts.Taken(InLayer(0));
            counts.Returned(InLayer(0));

            Assert.That(counts.ClearedShare(0), Is.EqualTo(0.5f));
            Assert.That(counts.OnMountainIn(0), Is.EqualTo(2));
            Assert.That(counts.Total, Is.EqualTo(2));
        }

        [Test]
        public void ClearedShare_LayerWithoutEmbers_CountsAsCleared()
        {
            LayerEmberCounts counts = new LayerEmberCounts(2);

            Assert.That(counts.ClearedShare(1), Is.EqualTo(1f));
            Assert.That(counts.ClearedShare(7), Is.EqualTo(1f), "unknown layer");
        }

        [Test]
        public void AddGenerated_CountsTowardInitialAndOnMountain()
        {
            LayerEmberCounts counts = new LayerEmberCounts(3);

            counts.AddGenerated(InLayer(2));

            Assert.That(counts.InitialIn(2), Is.EqualTo(1));
            Assert.That(counts.OnMountainIn(2), Is.EqualTo(1));
            Assert.That(counts.ClearedShare(2), Is.Zero);
        }

        [Test]
        public void ClearOnMountain_KeepsInitial()
        {
            LayerEmberCounts counts = new LayerEmberCounts(1);
            counts.AddGenerated(InLayer(0));

            counts.ClearOnMountain();

            Assert.That(counts.Total, Is.Zero);
            Assert.That(counts.InitialIn(0), Is.EqualTo(1));
            Assert.That(counts.ClearedShare(0), Is.EqualTo(1f));
        }

        [Test]
        public void EmberOfALayerBeyondTheCount_OnlyChangesTheTotal()
        {
            LayerEmberCounts counts = new LayerEmberCounts(1);

            counts.Returned(InLayer(5));

            Assert.That(counts.Total, Is.EqualTo(1));
            Assert.That(counts.OnMountainIn(0), Is.Zero);
        }
    }
}
