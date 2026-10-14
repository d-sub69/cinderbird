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
        [Tooltip("Embers up to this far beyond the carved sphere are uncovered by the scoop (it reached their top or middle): " +
                 "they are left lying loose in the hole, glowing, to be picked up. In meters.")]
        [SerializeField, Min(0f)] private float _emberExposeMargin = 0.25f;
        [Tooltip("A loose ember this many meters behind the ash surface in the aim line still counts as aimed at, so one " +
                 "half sunk into the ash can be picked up.")]
        [SerializeField, Min(0f)] private float _looseEmberAimDepth = 0.15f;

        public float Reach => _reach;
        public float Radius => _radius;
        public float Penetration => _penetration;
        public float EmberExposeMargin => _emberExposeMargin;
        public float LooseEmberAimDepth => _looseEmberAimDepth;
    }
}
