using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>Where a buried ember shows through the pile's surface.</summary>
    public readonly struct GlimmerPoint
    {
        public GlimmerPoint(Vector3 surfacePosition, float depth, int tierIndex, Vector3 emberPosition)
        {
            SurfacePosition = surfacePosition;
            Depth = depth;
            TierIndex = tierIndex;
            EmberPosition = emberPosition;
        }

        /// <summary>Chunk-local point on the ash surface closest to the ember (as probed).</summary>
        public Vector3 SurfacePosition { get; }

        /// <summary>Meters of ash between the ember and that surface point.</summary>
        public float Depth { get; }

        public int TierIndex { get; }

        /// <summary>The buried ember this glimmer belongs to; lets a dig re-check only the embers near it.</summary>
        public Vector3 EmberPosition { get; }
    }
}
