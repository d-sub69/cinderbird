namespace Cinderbird.Pacing
{
    /// <summary>Where a simulated run stands at a moment, and how fast it is going.</summary>
    public readonly struct PaceSample
    {
        public PaceSample(double seconds, int layer, int heatLevel, long glow, float ashPerMinute, float embersPerMinute,
            float glowPerMinute)
        {
            Seconds = seconds;
            Layer = layer;
            HeatLevel = heatLevel;
            Glow = glow;
            AshPerMinute = ashPerMinute;
            EmbersPerMinute = embersPerMinute;
            GlowPerMinute = glowPerMinute;
        }

        public double Seconds { get; }

        /// <summary>The deepest open layer (index).</summary>
        public int Layer { get; }

        public int HeatLevel { get; }

        /// <summary>Glow in hand.</summary>
        public long Glow { get; }

        /// <summary>Cubic meters of ash moved per minute since the last sample.</summary>
        public float AshPerMinute { get; }

        public float EmbersPerMinute { get; }

        /// <summary>Glow earned per minute since the last sample.</summary>
        public float GlowPerMinute { get; }
    }
}
