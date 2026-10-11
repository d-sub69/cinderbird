namespace Cinderbird.Terrain
{
    /// <summary>
    /// Outcome of one dig. The extracted embers themselves go into a caller-supplied list to avoid allocations.
    /// </summary>
    public readonly struct DigResult
    {
        public DigResult(float removedVolume, int emberCount)
        {
            RemovedVolume = removedVolume;
            EmberCount = emberCount;
        }

        /// <summary>Cubic meters of ash taken out of the grid.</summary>
        public float RemovedVolume { get; }

        /// <summary>Embers that were inside the dug volume.</summary>
        public int EmberCount { get; }

        public bool RemovedAnyAsh => RemovedVolume > 0f;
    }
}
