using System;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>
    /// Turns a <see cref="SaveFile"/> into JSON and back, checking the version first. Unity's JsonUtility may be
    /// used from background threads, so saving can serialize off the main thread.
    /// </summary>
    public static class SaveSerializer
    {
        public static string ToJson(SaveFile file)
        {
            // Pretty-printed: the terrain is one packed string anyway, so readability costs little.
            return JsonUtility.ToJson(file, true);
        }

        /// <param name="error">Why the text couldn't be read; null on success.</param>
        public static bool TryParse(string json, out SaveFile file, out string error)
        {
            file = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "the file is empty";
                return false;
            }

            try
            {
                SaveHeader header = JsonUtility.FromJson<SaveHeader>(json);
                if (header == null || header.Version <= 0)
                {
                    error = "it isn't a Cinderbird save";
                    return false;
                }

                if (header.Version > SaveFile.CurrentVersion)
                {
                    error = $"it was made by a newer version of the game (save version {header.Version})";
                    return false;
                }

                file = JsonUtility.FromJson<SaveFile>(SaveMigrator.Upgrade(json, header.Version));
                if (file == null)
                {
                    error = "it isn't a Cinderbird save";
                    return false;
                }

                file.Version = SaveFile.CurrentVersion;
                error = null;
                return true;
            }
            catch (ArgumentException e)
            {
                // JsonUtility reports malformed JSON this way.
                file = null;
                error = "the file is damaged (" + e.Message + ")";
                return false;
            }
        }
    }
}
