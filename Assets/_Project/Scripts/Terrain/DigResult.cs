namespace Cinderbird.Terrain
{
    /// <summary>
    /// Outcome of one dig. The extracted embers themselves go into a caller-supplied list to avoid allocations.
    /// </summary>
    public readonly struct DigResult
    {
        public DigResult(float removedVolume, int emberCount, int blockedLayer = -1)
        {
            RemovedVolume = removedVolume;
            EmberCount = emberCount;
            BlockedLayer = blockedLayer;
        }

        /// <summary>Cubic meters of ash taken out of the grid.</summary>
        public float RemovedVolume { get; }

        /// <summary>Embers that were inside the dug volume.</summary>
        public int EmberCount { get; }

        /// <summary>A locked layer the dig ran into and left untouched, or -1.</summary>
        public int BlockedLayer { get; }

        public bool RemovedAnyAsh => RemovedVolume > 0f;

        public bool WasBlocked => BlockedLayer >= 0;
    }
}
