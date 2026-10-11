namespace Cinderbird.Terrain
{
    /// <summary>
    /// Plain-data description of the starting mound, so <see cref="AshPileGenerator"/> can be tested
    /// without a ScriptableObject.
    /// </summary>
    public readonly struct AshPileShape
    {
        public AshPileShape(float radius, float height, float noiseAmplitude, float noiseScale, int noiseSeed)
        {
            Radius = radius;
            Height = height;
            NoiseAmplitude = noiseAmplitude;
            NoiseScale = noiseScale;
            NoiseSeed = noiseSeed;
        }

        /// <summary>Meters from the center to where the mound meets the floor.</summary>
        public float Radius { get; }

        /// <summary>Peak height in meters before noise.</summary>
        public float Height { get; }

        /// <summary>Max height variation in meters added by noise.</summary>
        public float NoiseAmplitude { get; }

        /// <summary>Noise frequency per meter; higher means smaller bumps.</summary>
        public float NoiseScale { get; }

        public int NoiseSeed { get; }
    }
}
