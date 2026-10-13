using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class LayerColorMixTests
    {
        private static readonly Color[] Colors = { Color.red, Color.green, Color.blue };
        private static readonly Color Fallback = Color.grey;

        [Test]
        public void Blend_OneLayer_GivesItsColor()
        {
            Color result = LayerColorMix.Blend(new[] { 0f, 2f, 0f }, Colors, Fallback);

            Assert.That(result, Is.EqualTo(Color.green));
        }

        [Test]
        public void Blend_TwoLayers_WeightsByAmount()
        {
            Color result = LayerColorMix.Blend(new[] { 3f, 1f, 0f }, Colors, Fallback);

            Assert.That(result.r, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(result.g, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(result.b, Is.Zero);
        }

        [Test]
        public void Blend_NothingTaken_GivesFallback()
        {
            Assert.That(LayerColorMix.Blend(new[] { 0f, 0f, 0f }, Colors, Fallback), Is.EqualTo(Fallback));
        }

        [Test]
        public void Blend_MoreAmountsThanColors_IgnoresTheExtra()
        {
            Color result = LayerColorMix.Blend(new[] { 0f, 0f, 1f, 5f }, Colors, Fallback);

            Assert.That(result, Is.EqualTo(Color.blue));
        }

        [Test]
        public void ScoopTransfer_HandsTheColorsToTheSieve()
        {
            ScoopTransfer transfer = new ScoopTransfer();
            SieveContents sieve = new SieveContents();
            Color ash = new Color(0.5f, 0.4f, 0.3f, 1f);
            Color dust = new Color(0.8f, 0.7f, 0.6f, 0.8f);

            transfer.TryBegin(0.1f, new List<Ember>(), 0.2f, ash, dust);
            transfer.HandOverTo(sieve);

            Assert.That(sieve.AshColor, Is.EqualTo(ash));
            Assert.That(sieve.DustColor, Is.EqualTo(dust));
        }
    }
}
