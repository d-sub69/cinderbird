using System;

namespace Cinderbird.Terrain
{
    /// <summary>One layer's ember plan: how many glowing clusters, how big, and how many embers sprinkled on their own.</summary>
    public readonly struct ClusterPlan
    {
        private readonly float[] _tierWeights;

        public ClusterPlan(int clusters, int embersPerCluster, float radius, int sprinkle, float[] tierWeights)
        {
            Clusters = Math.Max(0, clusters);
            EmbersPerCluster = Math.Max(1, embersPerCluster);
            Radius = Math.Max(0.1f, radius);
            Sprinkle = Math.Max(0, sprinkle);
            _tierWeights = tierWeights ?? Array.Empty<float>();
        }

        public int Clusters { get; }

        /// <summary>Embers in an average cluster.</summary>
        public int EmbersPerCluster { get; }

        /// <summary>Radius of an average cluster, in meters.</summary>
        public float Radius { get; }

        /// <summary>Embers sprinkled through the layer on their own.</summary>
        public int Sprinkle { get; }

        /// <summary>Relative chance of each ember tier in this layer.</summary>
        public float[] TierWeights => _tierWeights;

        /// <summary>The plan that buries <paramref name="embers"/>, the <paramref name="clusterShare"/> of them in clusters.</summary>
        public static ClusterPlan FromCount(int embers, float clusterShare, int embersPerCluster, float radius, float[] tierWeights)
        {
            int inClusters = (int)Math.Round(Math.Max(0, embers) * Math.Max(0f, Math.Min(1f, clusterShare)));
            int perCluster = Math.Max(1, embersPerCluster);
            int clusters = inClusters > 0 ? Math.Max(1, (int)Math.Round((double)inClusters / perCluster)) : 0;
            return new ClusterPlan(clusters, perCluster, radius, Math.Max(0, embers) - clusters * perCluster, tierWeights);
        }
    }
}
