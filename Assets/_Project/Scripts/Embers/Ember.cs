using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// One ember as plain data. No object references, so collections of these can move to Burst later.
    /// </summary>
    public readonly struct Ember
    {
        public Ember(Vector3 position, int tierIndex)
        {
            Position = position;
            TierIndex = tierIndex;
        }

        /// <summary>Chunk-local position while buried in the pile.</summary>
        public Vector3 Position { get; }

        /// <summary>Index into the <see cref="EmberTierCatalog"/>.</summary>
        public int TierIndex { get; }
    }
}
