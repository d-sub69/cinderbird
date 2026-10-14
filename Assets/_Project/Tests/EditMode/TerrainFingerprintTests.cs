using System.Globalization;
using System.Threading;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class TerrainFingerprintTests
    {
        private VoxelTerrainSettings _settings;
        private CultureInfo _culture;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<VoxelTerrainSettings>();
            _culture = Thread.CurrentThread.CurrentCulture;
        }

        [TearDown]
        public void TearDown()
        {
            Thread.CurrentThread.CurrentCulture = _culture;
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Build_SameSettings_SameTextWhateverTheNumberFormat()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            string invariant = TerrainFingerprint.Build(_settings, null);
            // German writes 0,25 for 0.25: a save made there must still fit the same mountain elsewhere.
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            string german = TerrainFingerprint.Build(_settings, null);

            Assert.That(german, Is.EqualTo(invariant));
            Assert.That(invariant, Does.Not.Contain(","));
        }

        [Test]
        public void Build_WithoutLayers_HasNoLayerPart()
        {
            Assert.That(TerrainFingerprint.Build(_settings, null), Does.Not.Contain("layers"));
        }
    }
}
