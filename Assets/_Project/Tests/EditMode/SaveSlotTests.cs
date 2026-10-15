using System.Collections.Generic;
using System.IO;
using Cinderbird.Embers;
using Cinderbird.Player;
using Cinderbird.Save;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class SaveSlotTests
    {
        private string _folder;
        private SaveFileStore _store;
        private SaveSlot _slot;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CinderbirdTests", Path.GetRandomFileName());
            _store = new SaveFileStore(_folder, "save.json");
            _slot = new SaveSlot(_store);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        private static SaveFile CreateFile(int glow)
        {
            SaveFile file = new SaveFile { SavedAtUtc = "2026-10-13T12:00:00Z" };
            file.Terrain.Fingerprint = "test mountain";
            file.Terrain.DeepestUnlockedLayer = 2;
            file.Currencies.Glow = glow;
            file.Currencies.Heat = 7;
            file.Player.Pose = new PlayerPose(new Vector3(1f, 2f, 3f), 90f, -10f, true);
            file.Tools.Slots.AddRange(new[] { "Sieve", string.Empty, "Basket" });
            file.Tools.ActiveSlot = 2;
            file.Tools.Placed.Add(new PlacedToolSave("Basket", new Vector3(4f, 0.3f, 5f), Quaternion.Euler(0f, 45f, 0f)));
            file.Sieve.AshVolume = 0.05f;
            file.Sieve.LoadVolume = 0.2f;
            file.Sieve.AshColor = new Color(0.6f, 0.4f, 0.3f, 1f);
            file.Sieve.DustColor = new Color(0.8f, 0.7f, 0.6f, 0.8f);
            EmberRecord.Fill(file.Sieve.Embers, new List<Ember> { new Ember(new Vector3(0.1f, 0f, 0.2f), 3, 2) });
            EmberRecord.Fill(file.Basket.Embers, new List<Ember> { new Ember(Vector3.zero, 1), new Ember(Vector3.one, 4, 1) });
            return file;
        }

        private static TerrainSnapshot CreateTerrainSnapshot()
        {
            // 2 × 1 × 1 m, two chunks side by side, a small mound in the middle.
            VoxelWorld world = new VoxelWorld(4, 2, 1, 1, 0.25f, new Vector3(-1f, -0.25f, -0.5f));
            AshTerrain terrain = new AshTerrain(world);
            terrain.Generate(new AshPileShape(0.8f, 0.6f, 0f, 0f, 1));
            terrain.Dig(new Vector3(0f, 0.3f, 0f), 0.3f, new List<Ember>());
            return TerrainSnapshot.Capture(terrain);
        }

        [Test]
        public void NoSave_LoadsNothing_WithoutWarning()
        {
            bool loaded = _slot.TryLoad(out SaveFile file, out TerrainSnapshot terrain, out string warning);

            Assert.That(loaded, Is.False);
            Assert.That(file, Is.Null);
            Assert.That(terrain, Is.Null);
            Assert.That(warning, Is.Null);
        }

        [Test]
        public void WriteThenLoad_KeepsEverything()
        {
            _slot.Write(CreateFile(42), CreateTerrainSnapshot());

            bool loaded = _slot.TryLoad(out SaveFile file, out TerrainSnapshot terrain, out string warning);

            Assert.That(loaded, Is.True);
            Assert.That(warning, Is.Null);
            Assert.That(file.Version, Is.EqualTo(SaveFile.CurrentVersion));
            Assert.That(file.Terrain.Fingerprint, Is.EqualTo("test mountain"));
            Assert.That(file.Terrain.DeepestUnlockedLayer, Is.EqualTo(2));
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.ChunkCount, Is.EqualTo(file.Terrain.ChunkCount).And.GreaterThan(0));

            Assert.That(file.Currencies.Glow, Is.EqualTo(42));
            Assert.That(file.Currencies.Heat, Is.EqualTo(7));

            PlayerPose pose = file.Player.Pose;
            Assert.That(pose.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(pose.Yaw, Is.EqualTo(90f));
            Assert.That(pose.Pitch, Is.EqualTo(-10f));
            Assert.That(pose.Crouching, Is.True);

            Assert.That(file.Tools.Slots, Is.EqualTo(new[] { "Sieve", string.Empty, "Basket" }));
            Assert.That(file.Tools.ActiveSlot, Is.EqualTo(2));
            Assert.That(file.Tools.Placed.Count, Is.EqualTo(1));
            Assert.That(file.Tools.Placed[0].ToolId, Is.EqualTo("Basket"));
            Assert.That(file.Tools.Placed[0].Position, Is.EqualTo(new Vector3(4f, 0.3f, 5f)));

            Assert.That(file.Sieve.AshVolume, Is.EqualTo(0.05f));
            Assert.That(file.Sieve.LoadVolume, Is.EqualTo(0.2f));
            Assert.That(file.Sieve.DustColor, Is.EqualTo(new Color(0.8f, 0.7f, 0.6f, 0.8f)));
            Ember sieveEmber = file.Sieve.Embers[0].ToEmber();
            Assert.That(sieveEmber.TierIndex, Is.EqualTo(3));
            Assert.That(sieveEmber.Layer, Is.EqualTo(2));
            Assert.That(EmberRecord.ToEmbers(file.Basket.Embers).Count, Is.EqualTo(2));
        }

        [Test]
        public void DamagedSave_FallsBackToTheBackup_WithAWarning()
        {
            _slot.Write(CreateFile(1), null);
            _slot.Write(CreateFile(2), null);
            File.WriteAllText(_store.MainPath, "{ this is not json");

            bool loaded = _slot.TryLoad(out SaveFile file, out _, out string warning);

            Assert.That(loaded, Is.True);
            Assert.That(file.Currencies.Glow, Is.EqualTo(1), "The backup holds the previous save");
            Assert.That(warning, Is.Not.Null);
        }

        [Test]
        public void DamagedTerrainData_CountsAsDamaged()
        {
            _slot.Write(CreateFile(1), null);
            SaveFile broken = CreateFile(2);
            broken.Terrain.Chunks = "AAAA";
            _slot.Write(broken, null);

            bool loaded = _slot.TryLoad(out SaveFile file, out _, out string warning);

            Assert.That(loaded, Is.True);
            Assert.That(file.Currencies.Glow, Is.EqualTo(1));
            Assert.That(warning, Is.Not.Null);
        }

        [Test]
        public void SaveAndBackupDamaged_StartsFresh_AndKeepsTheFilesAside()
        {
            _slot.Write(CreateFile(1), null);
            _slot.Write(CreateFile(2), null);
            File.WriteAllText(_store.MainPath, "garbage");
            File.WriteAllText(_store.BackupPath, string.Empty);

            bool loaded = _slot.TryLoad(out SaveFile file, out _, out string warning);

            Assert.That(loaded, Is.False);
            Assert.That(file, Is.Null);
            Assert.That(warning, Is.Not.Null);
            Assert.That(_store.HasAnySave, Is.False);
            Assert.That(Directory.GetFiles(_folder, "*.damaged-*").Length, Is.EqualTo(2));
        }

        [Test]
        public void SaveFromANewerVersion_IsRefused()
        {
            bool parsed = SaveSerializer.TryParse("{\"_version\": 999}", out SaveFile file, out string error);

            Assert.That(parsed, Is.False);
            Assert.That(file, Is.Null);
            Assert.That(error, Does.Contain("newer"));
        }

        [Test]
        public void JsonWithoutAVersion_IsRefused()
        {
            Assert.That(SaveSerializer.TryParse("{\"glow\": 5}", out _, out _), Is.False);
            Assert.That(SaveSerializer.TryParse("   ", out _, out _), Is.False);
        }

        [Test]
        public void Migrator_LeavesACurrentSaveUnchanged()
        {
            const string json = "{\"_version\": 1}";

            Assert.That(SaveMigrator.Upgrade(json, SaveFile.CurrentVersion), Is.EqualTo(json));
        }

        [Test]
        public void Delete_RemovesTheSave()
        {
            _slot.Write(CreateFile(1), null);

            _slot.Delete();

            Assert.That(_slot.TryLoad(out _, out _, out _), Is.False);
        }

        [Test]
        public void LooseEmbers_SurviveSaveAndLoad()
        {
            SaveFile file = CreateFile(3);
            EmberRecord.Fill(file.Terrain.LooseEmbers, new List<Ember> { new Ember(new Vector3(1f, 2f, 3f), 2, 1) });

            _slot.Write(file, null);
            _slot.TryLoad(out SaveFile loaded, out _, out _);

            List<Ember> loose = EmberRecord.ToEmbers(loaded.Terrain.LooseEmbers);
            Assert.That(loose.Count, Is.EqualTo(1));
            Assert.That(loose[0].Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(loose[0].TierIndex, Is.EqualTo(2));
        }

        [Test]
        public void Version1Save_StillLoads_WithNoLooseEmbers()
        {
            // A version 1 save has no loose-ember key at all.
            string json = SaveSerializer.ToJson(CreateFile(5))
                .Replace($"\"_version\": {SaveFile.CurrentVersion}", "\"_version\": 1")
                .Replace("\"_looseEmbers\": []", "\"_unused\": []");

            bool parsed = SaveSerializer.TryParse(json, out SaveFile file, out string error);

            Assert.That(json, Does.Contain("\"_version\": 1"), "the test really made a version 1 save");

            Assert.That(parsed, Is.True, error);
            Assert.That(file.Version, Is.EqualTo(SaveFile.CurrentVersion));
            Assert.That(file.Currencies.Glow, Is.EqualTo(5));
            Assert.That(file.Terrain.LooseEmbers, Is.Empty);
        }

        [Test]
        public void Upgrades_SurviveSaveAndLoad()
        {
            SaveFile file = CreateFile(3);
            file.Upgrades.Levels.Add(new UpgradeLevelSave("sieve", 2));
            file.Upgrades.Levels.Add(new UpgradeLevelSave("mesh", 1));

            _slot.Write(file, null);
            _slot.TryLoad(out SaveFile loaded, out _, out _);

            Assert.That(loaded.Upgrades.Levels.Count, Is.EqualTo(2));
            Assert.That(loaded.Upgrades.Levels[0].Id, Is.EqualTo("sieve"));
            Assert.That(loaded.Upgrades.Levels[0].Level, Is.EqualTo(2));
            Assert.That(loaded.Upgrades.Levels[1].Id, Is.EqualTo("mesh"));
        }

        [Test]
        public void Version2Save_StillLoads_WithNothingBought()
        {
            // A version 2 save has no upgrades key at all.
            string json = SaveSerializer.ToJson(CreateFile(5))
                .Replace($"\"_version\": {SaveFile.CurrentVersion}", "\"_version\": 2")
                .Replace("\"_upgrades\"", "\"_unusedUpgrades\"");

            bool parsed = SaveSerializer.TryParse(json, out SaveFile file, out string error);

            Assert.That(json, Does.Contain("\"_version\": 2"), "the test really made a version 2 save");
            Assert.That(parsed, Is.True, error);
            Assert.That(file.Version, Is.EqualTo(SaveFile.CurrentVersion));
            Assert.That(file.Upgrades.Levels, Is.Empty);
        }
    }
}
