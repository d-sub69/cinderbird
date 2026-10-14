using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>One ash layer of the mountain: where it starts, how it looks, and which embers it holds.</summary>
    [CreateAssetMenu(fileName = "Layer", menuName = "Cinderbird/Ash Layer")]
    public sealed class LayerDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "Cold Crust";
        [Tooltip("Ash color: tints the cut faces of this layer and the ash scooped from it into the sieve.")]
        [SerializeField] private Color _tint = Color.grey;
        [Tooltip("Color of the dust this layer's ash sheds when shaken and when dug.")]
        [SerializeField] private Color _dustColor = AshColors.DefaultDust;
        [Tooltip("Depth below the mountain's original surface, in meters, where this layer begins. Ignored for the last (pocket) layer.")]
        [SerializeField, Min(0f)] private float _startDepth;

        [Header("Embers")]
        [SerializeField, Min(0f)] private float _embersPerCubicMeter = 3f;
        [Tooltip("Relative chance of each ember tier, in the tier catalog's order. 0 = never in this layer.")]
        [SerializeField] private float[] _tierWeights = { 100f };

        [Header("Opening (ignored for the outermost layer, which starts open)")]
        [Tooltip("Nest Heat level needed before this layer melts open.")]
        [SerializeField, Min(0)] private int _unlockHeatLevel;
        [Tooltip("Share of the embers of every layer above that must be out of the pile first (0.9 = 90%).")]
        [SerializeField, Range(0f, 1f)] private float _unlockClearedShare = 0.9f;

        public string DisplayName => _displayName;
        public Color Tint => _tint;
        public Color DustColor => _dustColor;
        public float StartDepth => _startDepth;
        public float EmbersPerCubicMeter => _embersPerCubicMeter;
        public int UnlockHeatLevel => _unlockHeatLevel;
        public float UnlockClearedShare => _unlockClearedShare;

        /// <summary>Tier weights padded or cut to <paramref name="tierCount"/> entries.</summary>
        public float[] TierWeights(int tierCount)
        {
            float[] weights = new float[tierCount];
            for (int i = 0; i < tierCount && _tierWeights != null && i < _tierWeights.Length; i++)
            {
                weights[i] = _tierWeights[i];
            }

            return weights;
        }
    }
}
