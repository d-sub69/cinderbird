using System;

namespace Cinderbird.Pacing
{
    /// <summary>One ash layer as the pacing simulator sees it: how much ash, which embers, and its opening rule.</summary>
    public sealed class PaceLayer
    {
        private readonly int[] _embersByTier;

        /// <param name="volume">Cubic meters of ash in the layer.</param>
        /// <param name="embersByTier">Embers buried in it, per tier index.</param>
        /// <param name="heatLevel">Heat level it needs to open.</param>
        /// <param name="clearedShare">Share of every layer above that must be cleared first (0..1).</param>
        public PaceLayer(string name, float volume, int[] embersByTier, int heatLevel, float clearedShare)
        {
            Name = name ?? string.Empty;
            Volume = Math.Max(0f, volume);
            _embersByTier = embersByTier ?? Array.Empty<int>();
            HeatLevel = heatLevel;
            ClearedShare = clearedShare;
        }

        public string Name { get; }

        public float Volume { get; }

        public int HeatLevel { get; }

        public float ClearedShare { get; }

        public int TierCount => _embersByTier.Length;

        public int EmbersOf(int tier)
        {
            return tier >= 0 && tier < _embersByTier.Length ? _embersByTier[tier] : 0;
        }

        public int TotalEmbers
        {
            get
            {
                int total = 0;
                foreach (int count in _embersByTier)
                {
                    total += count;
                }

                return total;
            }
        }
    }
}
