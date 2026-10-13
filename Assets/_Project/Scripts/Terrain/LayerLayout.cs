using System;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Where each ash layer sits: shells by depth below the mountain's original surface, plus the
    /// innermost layer as a pocket (the Heart Hollow) that overrides the shells.
    /// </summary>
    public sealed class LayerLayout
    {
        private readonly float[] _bandStartDepths;
        private readonly Vector3 _pocketCenter;
        private readonly float _pocketRadiusSquared;

        /// <param name="bandStartDepths">Depth in meters where each shell layer starts, ascending; the first is usually 0.</param>
        /// <param name="pocketCenter">Local center of the innermost pocket layer.</param>
        /// <param name="pocketRadius">Radius of the pocket; 0 for no pocket.</param>
        public LayerLayout(float[] bandStartDepths, Vector3 pocketCenter, float pocketRadius)
        {
            if (bandStartDepths == null || bandStartDepths.Length == 0)
            {
                throw new ArgumentException("At least one layer band is needed.", nameof(bandStartDepths));
            }

            _bandStartDepths = (float[])bandStartDepths.Clone();
            _pocketCenter = pocketCenter;
            _pocketRadiusSquared = pocketRadius > 0f ? pocketRadius * pocketRadius : -1f;
        }

        /// <summary>The pocket's layer index: right after the last shell.</summary>
        public int PocketLayer => _bandStartDepths.Length;

        public int LayerCount => _bandStartDepths.Length + 1;

        /// <param name="surfaceHeight">Height of the original surface above this position's column.</param>
        public byte LayerAt(Vector3 position, float surfaceHeight)
        {
            if ((position - _pocketCenter).sqrMagnitude <= _pocketRadiusSquared)
            {
                return (byte)PocketLayer;
            }

            float depth = surfaceHeight - position.y;
            for (int layer = _bandStartDepths.Length - 1; layer > 0; layer--)
            {
                if (depth >= _bandStartDepths[layer])
                {
                    return (byte)layer;
                }
            }

            return 0;
        }
    }
}
