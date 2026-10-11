using UnityEngine;

namespace Cinderbird.Embers
{
    [CreateAssetMenu(fileName = "GlimmerSettings", menuName = "Cinderbird/Glimmer Settings")]
    public sealed class GlimmerSettings : ScriptableObject
    {
        [Tooltip("Embers buried less than this many meters below the surface show a glimmer.")]
        [SerializeField, Min(0.01f)] private float _maxDepth = 0.35f;
        [Tooltip("Glimmer dots created up front; the shallowest embers get them first.")]
        [SerializeField, Min(1)] private int _maxGlimmers = 24;

        [Header("Look")]
        [Tooltip("Dot size relative to the ember's own visual size.")]
        [SerializeField, Min(0.01f)] private float _sizeScale = 0.6f;
        [Tooltip("Brightness relative to the ember's emission, for an ember right at the surface.")]
        [SerializeField, Min(0f)] private float _brightness = 0.6f;
        [Tooltip("Brightness for an ember at max depth, as a fraction of the above.")]
        [SerializeField, Range(0f, 1f)] private float _deepBrightness = 0.2f;
        [Tooltip("Twinkles per second. 0 = steady glow.")]
        [SerializeField, Min(0f)] private float _twinkleSpeed = 1.5f;
        [Tooltip("How much the twinkle dims the glow, 0..1.")]
        [SerializeField, Range(0f, 1f)] private float _twinkleAmount = 0.5f;

        public float MaxDepth => _maxDepth;
        public int MaxGlimmers => _maxGlimmers;
        public float SizeScale => _sizeScale;
        public float Brightness => _brightness;
        public float DeepBrightness => _deepBrightness;
        public float TwinkleSpeed => _twinkleSpeed;
        public float TwinkleAmount => _twinkleAmount;
    }
}
