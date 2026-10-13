using UnityEngine;

namespace Cinderbird.Sifting
{
    [CreateAssetMenu(fileName = "SieveSettings", menuName = "Cinderbird/Sieve Settings")]
    public sealed class SieveSettings : ScriptableObject
    {
        [Header("Capacity")]
        [Tooltip("Scooping is refused once the sieve holds this many embers. Keep it a few below Max Visible Embers: " +
                 "the last scoop can bring in a couple extra, and those should still be drawn.")]
        [SerializeField, Min(1)] private int _emberCapacity = 16;

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

        [Header("Filling (ash pouring in from the pile)")]
        [Tooltip("Seconds from the click to the load fully in the sieve.")]
        [SerializeField, Min(0f)] private float _fillDuration = 0.45f;
        [Tooltip("Flying ash clumps per liter scooped (at least a few, at most Max Clumps).")]
        [SerializeField, Min(0f)] private float _clumpsPerLiter = 0.1f;
        [SerializeField, Min(1)] private int _maxClumps = 24;
        [Tooltip("Peak height of each clump's arc above the straight line, in meters.")]
        [SerializeField, Min(0f)] private float _clumpArcHeight = 0.35f;
        [Tooltip("Diameter of a clump in meters.")]
        [SerializeField, Min(0.005f)] private float _clumpSize = 0.05f;
        [Tooltip("Dust particles puffed up at the dig spot.")]
        [SerializeField, Min(0)] private int _digPuffParticles = 25;

        public float FillDuration => _fillDuration;
        public float ClumpsPerLiter => _clumpsPerLiter;
        public int MaxClumps => _maxClumps;
        public float ClumpArcHeight => _clumpArcHeight;
        public float ClumpSize => _clumpSize;
        public int DigPuffParticles => _digPuffParticles;

        public int MaxVisibleEmbers => _maxVisibleEmbers;
        public int EmberCapacity => _emberCapacity;
        public float Radius => _radius;
        public float SpotJitter => _spotJitter;
        public float FullAshHeight => _fullAshHeight;
        public float ReferenceAshVolume => _referenceAshVolume;
    }
}
