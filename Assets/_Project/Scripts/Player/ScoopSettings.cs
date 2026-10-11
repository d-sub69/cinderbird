using UnityEngine;

namespace Cinderbird.Player
{
    [CreateAssetMenu(fileName = "ScoopSettings", menuName = "Cinderbird/Scoop Settings")]
    public sealed class ScoopSettings : ScriptableObject
    {
        [Tooltip("How far from the camera the player can scoop, in meters.")]
        [SerializeField, Min(0.1f)] private float _reach = 3f;
        [Tooltip("Radius of the carved sphere in meters.")]
        [SerializeField, Min(0.05f)] private float _radius = 0.4f;
        [Tooltip("How far the sphere's center goes past the surface, as a fraction of the radius. " +
                 "0 = centered on the surface (shallow), 1 = whole sphere below it (deep).")]
        [SerializeField, Range(0f, 1f)] private float _penetration = 0.5f;

        public float Reach => _reach;
        public float Radius => _radius;
        public float Penetration => _penetration;
    }
}
