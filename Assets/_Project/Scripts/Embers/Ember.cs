using System;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// One ember as plain data. No object references, so collections of these can move to Burst later.
    /// </summary>
    public readonly struct Ember : IEquatable<Ember>
    {
        public Ember(Vector3 position, int tierIndex, byte layer = 0, ushort cluster = 0)
        {
            Position = position;
            TierIndex = tierIndex;
            Layer = layer;
            Cluster = cluster;
        }

        /// <summary>Terrain-local position while buried in the pile.</summary>
        public Vector3 Position { get; }

        /// <summary>Index into the <see cref="EmberTierCatalog"/>.</summary>
        public int TierIndex { get; }

        /// <summary>Ash layer it was buried in; it can't be dug out while that layer is locked.</summary>
        public byte Layer { get; }

        /// <summary>The cluster it was buried in (1 and up), or 0 for an ember sprinkled on its own. Clusters glow.</summary>
        public ushort Cluster { get; }

        /// <summary>The same ember somewhere else (e.g. carried down by sliding ash): tier, layer and cluster stay.</summary>
        public Ember MovedTo(Vector3 position)
        {
            return new Ember(position, TierIndex, Layer, Cluster);
        }

        /// <summary>Same spot, tier, layer and cluster: the same ember (exact comparison, no tolerance).</summary>
        public bool Equals(Ember other)
        {
            return Position.Equals(other.Position) && TierIndex == other.TierIndex && Layer == other.Layer && Cluster == other.Cluster;
        }

        public override bool Equals(object obj)
        {
            return obj is Ember other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Position, TierIndex, Layer, Cluster);
        }
    }
}
