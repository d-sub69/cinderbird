using System.IO;
using Cinderbird.Save;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class SaveFileStoreTests
    {
        private string _folder;
        private SaveFileStore _store;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CinderbirdTests", Path.GetRandomFileName());
            _store = new SaveFileStore(_folder, "save.json");
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
        public void Empty_HasNoSave()
        {
            Assert.That(_store.HasAnySave, Is.False);
            Assert.That(_store.ReadMain(), Is.Null);
            Assert.That(_store.ReadBackup(), Is.Null);
        }

        [Test]
        public void Write_CreatesTheFolderAndTheFile()
        {
            _store.Write("first");

            Assert.That(_store.ReadMain(), Is.EqualTo("first"));
            Assert.That(File.Exists(_store.TempPath), Is.False, "The temporary file should be gone");
        }

        [Test]
        public void Write_Again_KeepsThePreviousAsBackup()
        {
            _store.Write("first");
            _store.Write("second");

            Assert.That(_store.ReadMain(), Is.EqualTo("second"));
            Assert.That(_store.ReadBackup(), Is.EqualTo("first"));
        }

        [Test]
        public void Write_LeftoverTempFile_IsReplaced()
        {
            _store.Write("first");
            // As if the game had crashed halfway through writing the next save.
            File.WriteAllText(_store.TempPath, "half a sa");

            _store.Write("second");

            Assert.That(_store.ReadMain(), Is.EqualTo("second"));
        }

        [Test]
        public void Write_KeepsNonAsciiText()
        {
            _store.Write("Ëmber ✦");

            Assert.That(_store.ReadMain(), Is.EqualTo("Ëmber ✦"));
        }

        [Test]
        public void Quarantine_MovesBothFilesAside()
        {
            _store.Write("first");
            _store.Write("second");

            _store.Quarantine();

            Assert.That(_store.HasAnySave, Is.False);
            Assert.That(Directory.GetFiles(_folder, "*.damaged-*").Length, Is.EqualTo(2));
        }

        [Test]
        public void Delete_RemovesEverything()
        {
            _store.Write("first");
            _store.Write("second");

            _store.Delete();

            Assert.That(_store.HasAnySave, Is.False);
            Assert.That(File.Exists(_store.TempPath), Is.False);
        }
    }
}
