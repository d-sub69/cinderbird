namespace Cinderbird.Terrain
{
    /// <summary>
    /// Outcome of one dig. Embers found in the removed volume will be added here in step 3.
    /// </summary>
    public readonly struct DigResult
    {
        public DigResult(float removedVolume)
        {
            RemovedVolume = removedVolume;
        }

        /// <summary>Cubic meters of ash taken out of the grid.</summary>
        public float RemovedVolume { get; }

        public bool RemovedAnyAsh => RemovedVolume > 0f;
    }
}
