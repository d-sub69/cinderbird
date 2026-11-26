using System;

namespace Cinderbird.Pacing
{
    /// <summary>
    /// One ash layer as the pacing simulator sees it: how much ash, how much of it is vein, which embers lie in the clusters
    /// and which between them, and its opening rule.
    /// </summary>
    public sealed class PaceLayer
    {
        private readonly int[] _embersByTier;
        private readonly int[] _clusterEmbersByTier;

        /// <param name="volume">Cubic meters of ash in the layer.</param>
        /// <param name="embersByTier">Embers buried in it, per tier index.</param>
        /// <param name="heatLevel">Heat level it needs to open.</param>
        /// <param name="clearedShare">Share of every layer above that must be cleared first (0..1).</param>
        /// <param name="clusterVolume">Cubic meters of it that are cluster (0 = no clusters: embers spread evenly).</param>
        /// <param name="clusterEmbersByTier">Of <paramref name="embersByTier"/>, those inside the clusters.</param>
        public PaceLayer(string name, float volume, int[] embersByTier, int heatLevel, float clearedShare, float clusterVolume = 0f,
            int[] clusterEmbersByTier = null)
        {
            Name = name ?? string.Empty;
            Volume = Math.Max(0f, volume);
            _embersByTier = embersByTier ?? Array.Empty<int>();
            HeatLevel = heatLevel;
            ClearedShare = clearedShare;
            ClusterVolume = Math.Max(0f, Math.Min(Volume, clusterVolume));
            _clusterEmbersByTier = clusterEmbersByTier ?? new int[_embersByTier.Length];
        }

        public string Name { get; }

        public float Volume { get; }

        public float ClusterVolume { get; }

        public int HeatLevel { get; }

        public float ClearedShare { get; }

        public int TierCount => _embersByTier.Length;

        public int EmbersOf(int tier)
        {
            return tier >= 0 && tier < _embersByTier.Length ? _embersByTier[tier] : 0;
        }

        /// <summary>Embers of a tier inside the clusters (never more than <see cref="EmbersOf"/>).</summary>
        public int ClusterEmbersOf(int tier)
        {
            int inClusters = tier >= 0 && tier < _clusterEmbersByTier.Length ? _clusterEmbersByTier[tier] : 0;
            return Math.Min(inClusters, EmbersOf(tier));
        }

        public int TotalEmbers => Sum(_embersByTier);

        public int TotalClusterEmbers
        {
            get
            {
                int total = 0;
                for (int tier = 0; tier < _embersByTier.Length; tier++)
                {
                    total += ClusterEmbersOf(tier);
                }

                return total;
            }
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            foreach (int value in values)
            {
                total += value;
            }

            return total;
        }
    }
}
