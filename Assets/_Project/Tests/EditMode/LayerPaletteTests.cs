using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class LayerPaletteTests
    {
        [Test]
        public void WithoutLayerColors_EverythingIsTheDefault()
        {
            LayerPalette palette = new LayerPalette(null, null);

            palette.Mix(new[] { 1f }, out Color ash, out Color dust);

            Assert.That(ash, Is.EqualTo(AshColors.DefaultAsh));
            Assert.That(dust, Is.EqualTo(AshColors.DefaultDust));
            Assert.That(palette.DustOf(0), Is.EqualTo(AshColors.DefaultDust));
            Assert.That(palette.AshTints, Is.Null);
        }

        [Test]
        public void Mix_BlendsByAmountTakenPerLayer()
        {
            LayerPalette palette = new LayerPalette(new[] { Color.black, Color.white }, new[] { Color.red, Color.blue });

            palette.Mix(new[] { 1f, 1f }, out Color ash, out Color dust);

            Assert.That(ash.r, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(dust.r, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(dust.b, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void DustOf_UnknownLayer_GivesTheDefault()
        {
            LayerPalette palette = new LayerPalette(new[] { Color.black }, new[] { Color.red });

            Assert.That(palette.DustOf(0), Is.EqualTo(Color.red));
            Assert.That(palette.DustOf(3), Is.EqualTo(AshColors.DefaultDust));
            Assert.That(palette.DustOf(-1), Is.EqualTo(AshColors.DefaultDust));
        }
    }
}
