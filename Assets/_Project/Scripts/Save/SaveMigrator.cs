namespace Cinderbird.Save
{
    /// <summary>
    /// Upgrades older save layouts to <see cref="SaveFile.CurrentVersion"/> before parsing. Version 1 is the first
    /// layout, so there is nothing to upgrade yet. When the layout changes: bump the version, add a step here
    /// that rewrites the old JSON (e.g. renames a key), and add a test that loads an old save.
    /// </summary>
    public static class SaveMigrator
    {
        /// <param name="json">A save in layout <paramref name="fromVersion"/>.</param>
        /// <returns>The same save in the current layout.</returns>
        public static string Upgrade(string json, int fromVersion)
        {
            // One step per version, oldest first, each feeding the next, e.g.:
            // if (fromVersion <= 1) { json = UpgradeFrom1To2(json); }
            return json;
        }
    }
}
