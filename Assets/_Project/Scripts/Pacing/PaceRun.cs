using System.Collections.Generic;

namespace Cinderbird.Pacing
{
    /// <summary>The outcome of one simulated run: its moments, a sample every few minutes, and how it ended.</summary>
    public sealed class PaceRun
    {
        public PaceRun(int layerCount, int levelCount)
        {
            LayerOpenedAt = new double[layerCount];
            LevelReachedAt = new double[levelCount];
            for (int i = 0; i < layerCount; i++)
            {
                LayerOpenedAt[i] = i == 0 ? 0.0 : -1.0;
            }

            for (int i = 0; i < levelCount; i++)
            {
                LevelReachedAt[i] = -1.0;
            }
        }

        public List<PaceEvent> Events { get; } = new List<PaceEvent>();

        public List<PaceSample> Samples { get; } = new List<PaceSample>();

        /// <summary>Seconds into the run each layer opened; −1 if it never did.</summary>
        public double[] LayerOpenedAt { get; }

        /// <summary>Seconds into the run each Heat level (1, 2, … at index 0, 1, …) was reached; −1 if never.</summary>
        public double[] LevelReachedAt { get; }

        /// <summary>Seconds until the last layer was dug out (the end of the main path); −1 if it never was.</summary>
        public double FinishedAt { get; set; } = -1.0;

        /// <summary>Why the run stopped short, or empty when it finished.</summary>
        public string StopReason { get; set; } = string.Empty;

        public int Purchases { get; set; }

        public bool Finished => FinishedAt >= 0.0;
    }
}
