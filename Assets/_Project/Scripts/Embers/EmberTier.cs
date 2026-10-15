using UnityEngine;

namespace Cinderbird.Embers
{
    [CreateAssetMenu(fileName = "EmberTier", menuName = "Cinderbird/Ember Tier")]
    public sealed class EmberTier : ScriptableObject
    {
        [SerializeField] private string _displayName = "Cinder";

        [Header("Look")]
        [SerializeField] private Color _color = new Color(1f, 0.45f, 0.1f);
        [Tooltip("Emission multiplier; values above 1 make bloom pick it up.")]
        [SerializeField, Min(0f)] private float _emissionIntensity = 4f;
        [Tooltip("Diameter in meters when shown in the sieve or basket.")]
        [SerializeField, Min(0.005f)] private float _visualScale = 0.03f;

        [Header("Value")]
        [SerializeField, Min(0)] private int _glowValue = 1;
        [SerializeField, Min(0)] private int _heatValue = 1;

        [Header("Spawning")]
        [Tooltip("Relative chance compared to other tiers. 0 = never spawns.")]
        [SerializeField, Min(0f)] private float _spawnWeight = 80f;

        [Header("Sifting")]
        [Tooltip("Small enough to slip through a coarse sieve mesh while shaking (the mesh upgrade stops it).")]
        [SerializeField] private bool _slipsThroughMesh;

        public string DisplayName => _displayName;
        public Color Color => _color;
        public Color EmissionColor => _color * _emissionIntensity;
        public float VisualScale => _visualScale;
        public int GlowValue => _glowValue;
        public int HeatValue => _heatValue;
        public float SpawnWeight => _spawnWeight;
        public bool SlipsThroughMesh => _slipsThroughMesh;
    }
}
