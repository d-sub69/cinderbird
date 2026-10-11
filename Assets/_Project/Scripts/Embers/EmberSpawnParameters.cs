namespace Cinderbird.Embers
{
    /// <summary>
    /// Plain-data spawn tuning, so <see cref="EmberSpawner"/> can be tested without a ScriptableObject.
    /// </summary>
    public readonly struct EmberSpawnParameters
    {
        public EmberSpawnParameters(float embersPerCubicMeter, float minDensity, int seed)
        {
            EmbersPerCubicMeter = embersPerCubicMeter;
            MinDensity = minDensity;
            Seed = seed;
        }

        public float EmbersPerCubicMeter { get; }

        /// <summary>Embers only spawn where density is at least this, keeping them off the surface.</summary>
        public float MinDensity { get; }

        public int Seed { get; }
    }
}
