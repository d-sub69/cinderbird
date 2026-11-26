using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>A cluster as it stands now: the embers still buried in it, where they are, and how rich they are.</summary>
    public readonly struct ClusterState
    {
        public ClusterState(ushort id, byte layer, int count, float weight, Vector3 center, float radius, int bestTier)
        {
            Id = id;
            Layer = layer;
            Count = count;
            Weight = weight;
            Center = center;
            Radius = radius;
            BestTier = bestTier;
        }

        public ushort Id { get; }

        public byte Layer { get; }

        /// <summary>Embers still buried in it; 0 once it's emptied.</summary>
        public int Count { get; }

        /// <summary>How much it's worth, by its embers' tiers: what its glow follows.</summary>
        public float Weight { get; }

        /// <summary>Middle of its remaining embers (terrain-local).</summary>
        public Vector3 Center { get; }

        /// <summary>How far its remaining embers spread from the middle, as a sphere's radius.</summary>
        public float Radius { get; }

        /// <summary>The most valuable tier still in it (its glow color); −1 when empty.</summary>
        public int BestTier { get; }

        public bool IsEmpty => Count <= 0;
    }
}
