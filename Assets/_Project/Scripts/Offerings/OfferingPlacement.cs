using System;
using UnityEngine;

namespace Cinderbird.Offerings
{
    /// <summary>
    /// Where an offering is buried, independent of the exact mountain: the direction from the mountain's middle, how far
    /// out toward its foot, and how deep below the surface there. Resolved when the mountain is built.
    /// </summary>
    [Serializable]
    public struct OfferingPlacement
    {
        [Tooltip("Direction from the mountain's middle, in degrees: 0 = +Z (the terrain's forward), 90 = +X.")]
        [SerializeField] private float _angle;
        [Tooltip("How far out: 0 = the middle (summit), 1 = the mountain's foot.")]
        [SerializeField, Range(0f, 1f)] private float _outward;
        [Tooltip("Meters below the surface there.")]
        [SerializeField, Min(0f)] private float _depth;

        public OfferingPlacement(float angle, float outward, float depth)
        {
            _angle = angle;
            _outward = outward;
            _depth = depth;
        }

        public float Angle => _angle;
        public float Outward => _outward;
        public float Depth => _depth;

        /// <summary>The spot's x and z in the terrain's local space, for a mountain of this radius centered at 0, 0.</summary>
        public Vector3 HorizontalPosition(float pileRadius)
        {
            float radians = _angle * Mathf.Deg2Rad;
            float distance = Mathf.Clamp01(_outward) * pileRadius;
            return new Vector3(Mathf.Sin(radians) * distance, 0f, Mathf.Cos(radians) * distance);
        }
    }
}
