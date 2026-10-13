using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Cinderbird.Save
{
    /// <summary>
    /// Reads and writes one save file so that a crash or power cut mid-write can never leave it half-written:
    /// the new contents go to a temporary file first, which then replaces the save in one step, keeping the
    /// previous save as a backup. Plain file I/O with no Unity calls, so it's safe on a background thread.
    /// </summary>
    public sealed class SaveFileStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public SaveFileStore(string directory, string fileName)
        {
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("A save needs a folder and a file name.");
            }

            Directory = directory;
            MainPath = Path.Combine(directory, fileName);
            BackupPath = MainPath + ".bak";
            TempPath = MainPath + ".tmp";
        }

        public string Directory { get; }
        public string MainPath { get; }
        public string BackupPath { get; }
        public string TempPath { get; }

        public bool HasAnySave => File.Exists(MainPath) || File.Exists(BackupPath);

        public void Write(string contents)
        {
            System.IO.Directory.CreateDirectory(Directory);
            byte[] bytes = Utf8NoBom.GetBytes(contents);
            using (FileStream stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                // Push it to the disk itself, not just the OS cache, before the swap makes it the real save.
                stream.Flush(true);
            }

            if (File.Exists(MainPath))
            {
                File.Replace(TempPath, MainPath, BackupPath, true);
            }
            else
            {
                File.Move(TempPath, MainPath);
            }
        }

        /// <returns>The save's text, or null if there is none.</returns>
        public string ReadMain()
        {
            return ReadIfExists(MainPath);
        }

        /// <returns>The previous save's text, or null if there is none.</returns>
        public string ReadBackup()
        {
            return ReadIfExists(BackupPath);
        }

        /// <summary>Moves damaged saves aside (renamed with a time stamp) instead of deleting them, so they can be inspected.</summary>
        public void Quarantine()
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            MoveAside(MainPath, stamp);
            MoveAside(BackupPath, stamp);
        }

        /// <summary>Deletes the save, its backup and any leftover temporary file.</summary>
        public void Delete()
        {
            File.Delete(MainPath);
            File.Delete(BackupPath);
            File.Delete(TempPath);
        }

        private static string ReadIfExists(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path, Utf8NoBom) : null;
        }

        private static void MoveAside(string path, string stamp)
        {
            if (!File.Exists(path))
            {
                return;
            }

            string target = $"{path}.damaged-{stamp}";
            File.Delete(target);
            File.Move(path, target);
        }
    }
}
