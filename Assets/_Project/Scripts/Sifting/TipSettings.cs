using UnityEngine;

namespace Cinderbird.Sifting
{
    [CreateAssetMenu(fileName = "TipSettings", menuName = "Cinderbird/Tip Settings")]
    public sealed class TipSettings : ScriptableObject
    {
        [Header("Pour")]
        [Tooltip("Peak forward tilt of a tool when pouring, in degrees. Flip the sign if it tips toward you.")]
        [SerializeField] private float _tipAngle = 75f;
        [Tooltip("Seconds for the whole pour, out and back.")]
        [SerializeField, Min(0.05f)] private float _tipDuration = 0.5f;

        [Header("Refused tip")]
        [Tooltip("Size of the 'nope' wobble in degrees.")]
        [SerializeField] private float _refuseAngle = 10f;
        [SerializeField, Min(0.05f)] private float _refuseDuration = 0.35f;

        [Header("Embers landing in the basket")]
        [Tooltip("Seconds after the tip starts before embers appear above the basket.")]
        [SerializeField, Min(0f)] private float _dropDelay = 0.2f;
        [Tooltip("Height in meters the embers fall from.")]
        [SerializeField, Min(0f)] private float _dropHeight = 0.15f;
        [SerializeField, Min(0.01f)] private float _dropDuration = 0.25f;

        public float TipAngle => _tipAngle;
        public float TipDuration => _tipDuration;
        public float RefuseAngle => _refuseAngle;
        public float RefuseDuration => _refuseDuration;
        public float DropDelay => _dropDelay;
        public float DropHeight => _dropHeight;
        public float DropDuration => _dropDuration;
    }
}
