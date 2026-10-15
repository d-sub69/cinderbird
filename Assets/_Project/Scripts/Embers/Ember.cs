using System;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// One ember as plain data. No object references, so collections of these can move to Burst later.
    /// </summary>
    public readonly struct Ember : IEquatable<Ember>
    {
        public Ember(Vector3 position, int tierIndex, byte layer = 0)
        {
            Position = position;
            TierIndex = tierIndex;
            Layer = layer;
        }

        /// <summary>Chunk-local position while buried in the pile.</summary>
        public Vector3 Position { get; }

        /// <summary>Index into the <see cref="EmberTierCatalog"/>.</summary>
        public int TierIndex { get; }

        /// <summary>Ash layer it was buried in; it can't be dug out while that layer is locked.</summary>
        public byte Layer { get; }

        /// <summary>Same spot, tier and layer: the same ember (exact comparison, no tolerance).</summary>
        public bool Equals(Ember other)
        {
            return Position.Equals(other.Position) && TierIndex == other.TierIndex && Layer == other.Layer;
        }

        public override bool Equals(object obj)
        {
            return obj is Ember other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Position, TierIndex, Layer);
        }
    }
}
