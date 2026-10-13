using System;
using Cinderbird.Save;
using UnityEngine;

namespace Cinderbird.Options
{
    /// <summary>
    /// Reads and writes options.json through a <see cref="SaveFileStore"/> (atomic writes with a backup).
    /// A damaged file never stops the game: it falls back to the backup, then to the defaults.
    /// </summary>
    public sealed class OptionsStore
    {
        private readonly SaveFileStore _store;

        public OptionsStore(SaveFileStore store)
        {
            _store = store;
        }

        public SaveFileStore Store => _store;

        /// <param name="defaults">Used for keys the file doesn't have, or entirely if it can't be read. Not modified.</param>
        /// <param name="warning">Set when a file existed but couldn't be read; null otherwise.</param>
        public GameOptions Load(GameOptions defaults, out string warning)
        {
            warning = null;
            if (!_store.HasAnySave)
            {
                return defaults.Clone();
            }

            if (TryParse(_store.ReadMain(), defaults, out GameOptions options) ||
                TryParse(_store.ReadBackup(), defaults, out options))
            {
                return options;
            }

            _store.Quarantine();
            warning = "The options file couldn't be read; using the default options.";
            return defaults.Clone();
        }

        public void Save(GameOptions options)
        {
            _store.Write(JsonUtility.ToJson(options, true));
        }

        private static bool TryParse(string json, GameOptions defaults, out GameOptions options)
        {
            options = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            GameOptions candidate = defaults.Clone();
            try
            {
                // Overwrite rather than replace: keys missing from the file keep their defaults.
                JsonUtility.FromJsonOverwrite(json, candidate);
            }
            catch (ArgumentException)
            {
                return false;
            }

            options = candidate;
            return true;
        }
    }
}
