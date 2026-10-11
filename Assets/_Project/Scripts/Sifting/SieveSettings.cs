using UnityEngine;

namespace Cinderbird.Sifting
{
    [CreateAssetMenu(fileName = "SieveSettings", menuName = "Cinderbird/Sieve Settings")]
    public sealed class SieveSettings : ScriptableObject
    {
        [Header("Visuals")]
        [Tooltip("Ember objects created up front; embers beyond this are counted but not drawn.")]
        [SerializeField, Min(1)] private int _maxVisibleEmbers = 20;
        [Tooltip("Inner radius of the sieve in meters; embers are scattered within it.")]
        [SerializeField, Min(0.01f)] private float _radius = 0.2f;
        [Tooltip("How far ember spots stray from an even spiral, as a fraction of their spacing. " +
                 "Automatically reduced if the largest embers could touch.")]
        [SerializeField, Range(0f, 0.5f)] private float _spotJitter = 0.25f;
        [Tooltip("Height of the ash layer in meters when the sieve holds the reference volume.")]
        [SerializeField, Min(0.001f)] private float _fullAshHeight = 0.06f;
        [Tooltip("Ash volume (m³) drawn at full height. Visual only; it does not limit scooping.")]
        [SerializeField, Min(0.001f)] private float _referenceAshVolume = 0.2f;

        public int MaxVisibleEmbers => _maxVisibleEmbers;
        public float Radius => _radius;
        public float SpotJitter => _spotJitter;
        public float FullAshHeight => _fullAshHeight;
        public float ReferenceAshVolume => _referenceAshVolume;
    }
}
