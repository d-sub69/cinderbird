using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>How embers lying loose fall, land and get picked up.</summary>
    [CreateAssetMenu(fileName = "LooseEmberSettings", menuName = "Cinderbird/Loose Ember Settings")]
    public sealed class LooseEmberSettings : ScriptableObject
    {
        [Tooltip("Downward acceleration while falling, in m/s².")]
        [SerializeField, Min(0.1f)] private float _gravity = 9.81f;
        [Tooltip("Share of the landing speed kept for the bounce back up.")]
        [SerializeField, Range(0f, 0.9f)] private float _bounce = 0.3f;
        [Tooltip("Below this landing speed (m/s) an ember stops instead of bouncing.")]
        [SerializeField, Min(0f)] private float _minBounceSpeed = 1f;
        [Tooltip("How close to the aim line an ember must be to be picked up with the sieve, in meters.")]
        [SerializeField, Min(0.01f)] private float _pickTolerance = 0.25f;
        [Tooltip("Seconds between checks of every loose ember against the ash (also checked whenever the ash changes).")]
        [SerializeField, Min(0.05f)] private float _checkInterval = 0.5f;
        [Tooltip("How quickly a shown ember glides to where it lies now (per second; higher is snappier). Hides the steps " +
                 "in which sliding ash moves it, and shows an uncovered ember coming out of the ash.")]
        [SerializeField, Min(0.5f)] private float _followRate = 12f;

        public float Gravity => _gravity;
        public float Bounce => _bounce;
        public float MinBounceSpeed => _minBounceSpeed;
        public float PickTolerance => _pickTolerance;
        public float CheckInterval => _checkInterval;
        public float FollowRate => _followRate;

        public FallParameters Fall => new FallParameters(_gravity, _bounce, _minBounceSpeed);
    }
}
