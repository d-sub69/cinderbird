namespace Cinderbird.Save
{
    /// <summary>
    /// Upgrades older save layouts to <see cref="SaveFile.CurrentVersion"/> before parsing, one version at a time.
    /// When the layout changes: bump the version, add a step here that rewrites the old JSON if needed (e.g. a
    /// renamed key), and add a test that loads an old save.
    /// </summary>
    public static class SaveMigrator
    {
        /// <param name="json">A save in layout <paramref name="fromVersion"/>.</param>
        /// <returns>The same save in the current layout.</returns>
        public static string Upgrade(string json, int fromVersion)
        {
            // 1 → 2 added loose embers to the terrain save, 2 → 3 the upgrades. Nothing to rewrite: a missing key keeps its
            // default (no loose embers, nothing bought).
            return json;
        }
    }
}
