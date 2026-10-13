using System.Collections.Generic;
using System.IO;
using Cinderbird.Options;
using Cinderbird.Save;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class GameOptionsTests
    {
        private static readonly OptionLimits Limits = new OptionLimits(0.2f, 3f, 1f, 50f, 100f, 60f, 3, 2, new[] { 30, 60 });

        private string _folder;
        private SaveFileStore _files;
        private OptionsStore _store;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CinderbirdTests", Path.GetRandomFileName());
            _files = new SaveFileStore(_folder, "options.json");
            _store = new OptionsStore(_files);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [Test]
        public void Defaults_ComeFromTheLimits()
        {
            GameOptions options = GameOptions.CreateDefaults(Limits);

            Assert.That(options.MouseSensitivity, Is.EqualTo(1f));
            Assert.That(options.FieldOfView, Is.EqualTo(60f));
            Assert.That(options.QualityLevel, Is.EqualTo(2));
            Assert.That(options.VSync, Is.True);
            Assert.That(options.MasterVolume, Is.EqualTo(1f));
            Assert.That(options.HasResolution, Is.False, "Defaults use the display's own resolution");
        }

        [Test]
        public void Sanitize_ClampsEverythingIntoRange()
        {
            GameOptions options = GameOptions.CreateDefaults(Limits);
            options.MouseSensitivity = 50f;
            options.FieldOfView = 10f;
            options.MasterVolume = -2f;
            options.QualityLevel = 7;
            options.FrameRateLimit = -5;
            options.DisplayMode = (DisplayModeOption)42;
            options.ResolutionWidth = 1920;
            options.ResolutionHeight = -1;

            options.Sanitize(Limits);

            Assert.That(options.MouseSensitivity, Is.EqualTo(3f));
            Assert.That(options.FieldOfView, Is.EqualTo(50f));
            Assert.That(options.MasterVolume, Is.Zero);
            Assert.That(options.QualityLevel, Is.EqualTo(2), "Unknown level falls back to the default");
            Assert.That(options.FrameRateLimit, Is.Zero);
            Assert.That(options.DisplayMode, Is.EqualTo(DisplayModeOption.Borderless));
            Assert.That(options.HasResolution, Is.False);
        }

        [Test]
        public void Sanitize_NaN_GetsTheDefault()
        {
            GameOptions options = GameOptions.CreateDefaults(Limits);
            options.MouseSensitivity = float.NaN;

            options.Sanitize(Limits);

            Assert.That(options.MouseSensitivity, Is.EqualTo(1f));
        }

        [Test]
        public void CopyFrom_TakesEveryValue()
        {
            GameOptions changed = GameOptions.CreateDefaults(Limits);
            changed.InvertY = true;
            changed.FieldOfView = 90f;
            changed.ResolutionWidth = 1280;
            changed.ResolutionHeight = 720;
            GameOptions target = GameOptions.CreateDefaults(Limits);

            target.CopyFrom(changed);

            Assert.That(JsonUtility.ToJson(target), Is.EqualTo(JsonUtility.ToJson(changed)));
        }

        [Test]
        public void Store_NoFile_GivesDefaults()
        {
            GameOptions loaded = _store.Load(GameOptions.CreateDefaults(Limits), out string warning);

            Assert.That(warning, Is.Null);
            Assert.That(loaded.FieldOfView, Is.EqualTo(60f));
        }

        [Test]
        public void Store_SaveThenLoad_KeepsTheOptions()
        {
            GameOptions options = GameOptions.CreateDefaults(Limits);
            options.MouseSensitivity = 1.75f;
            options.InvertY = true;
            options.DisplayMode = DisplayModeOption.Windowed;
            options.ResolutionWidth = 1600;
            options.ResolutionHeight = 900;
            options.VSync = false;
            options.FrameRateLimit = 60;
            options.QualityLevel = 0;
            options.MasterVolume = 0.4f;

            _store.Save(options);
            GameOptions loaded = _store.Load(GameOptions.CreateDefaults(Limits), out string warning);

            Assert.That(warning, Is.Null);
            Assert.That(JsonUtility.ToJson(loaded), Is.EqualTo(JsonUtility.ToJson(options)));
        }

        [Test]
        public void Store_OlderFileWithoutAKey_KeepsThatKeysDefault()
        {
            File.WriteAllText(MainPath(), "{\"_version\": 1, \"_invertY\": true}");

            GameOptions loaded = _store.Load(GameOptions.CreateDefaults(Limits), out _);

            Assert.That(loaded.InvertY, Is.True);
            Assert.That(loaded.FieldOfView, Is.EqualTo(60f), "Missing from the file");
            Assert.That(loaded.QualityLevel, Is.EqualTo(2));
        }

        [Test]
        public void Store_DamagedFile_GivesDefaults_WithAWarning()
        {
            File.WriteAllText(MainPath(), "{ not json");

            GameOptions loaded = _store.Load(GameOptions.CreateDefaults(Limits), out string warning);

            Assert.That(warning, Is.Not.Null);
            Assert.That(loaded.FieldOfView, Is.EqualTo(60f));
            Assert.That(_files.HasAnySave, Is.False, "The damaged file is moved aside");
        }

        [Test]
        public void Store_Load_DoesNotChangeTheDefaultsObject()
        {
            GameOptions defaults = GameOptions.CreateDefaults(Limits);
            File.WriteAllText(MainPath(), "{\"_fieldOfView\": 80}");

            _store.Load(defaults, out _);

            Assert.That(defaults.FieldOfView, Is.EqualTo(60f));
        }

        [Test]
        public void ResolutionList_RemovesRepeats_AndSorts()
        {
            List<Vector2Int> sizes = ResolutionList.Unique(new[]
            {
                new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                new Vector2Int(1280, 1024), new Vector2Int(0, 0),
            });

            Assert.That(sizes, Is.EqualTo(new[] { new Vector2Int(1280, 720), new Vector2Int(1280, 1024), new Vector2Int(1920, 1080) }));
        }

        [Test]
        public void ResolutionList_IndexOfClosest_PrefersExact_ThenNearestPixelCount()
        {
            Vector2Int[] sizes = { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) };

            Assert.That(ResolutionList.IndexOfClosest(sizes, 1920, 1080), Is.EqualTo(1));
            Assert.That(ResolutionList.IndexOfClosest(sizes, 2000, 1100), Is.EqualTo(1));
            Assert.That(ResolutionList.IndexOfClosest(sizes, 3840, 2160), Is.EqualTo(2));
            Assert.That(ResolutionList.IndexOfClosest(new Vector2Int[0], 1, 1), Is.EqualTo(-1));
        }

        private string MainPath()
        {
            Directory.CreateDirectory(_folder);
            return _files.MainPath;
        }
    }
}
