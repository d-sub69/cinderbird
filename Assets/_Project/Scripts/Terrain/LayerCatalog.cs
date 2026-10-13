using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// The mountain's ash layers from the surface inward. The last layer is the Heart Hollow: a pocket at the
    /// bottom center holding exactly one special ember. Layer indices are stored in the terrain and saves,
    /// so don't reorder layers once save data exists.
    /// </summary>
    [CreateAssetMenu(fileName = "LayerCatalog", menuName = "Cinderbird/Ash Layer Catalog")]
    public sealed class LayerCatalog : ScriptableObject
    {
        [Tooltip("Outermost first; the last one is the pocket layer.")]
        [SerializeField] private List<LayerDefinition> _layers = new List<LayerDefinition>();

        [Header("Heart Hollow (last layer)")]
        [Tooltip("Height of the pocket's center above the mountain's base, in meters.")]
        [SerializeField, Min(0f)] private float _pocketHeight = 1.5f;
        [SerializeField, Min(0f)] private float _pocketRadius = 1.2f;
        [Tooltip("Tier of the single ember at the pocket's center (index in the tier catalog).")]
        [SerializeField, Min(0)] private int _heartTierIndex = 5;

        public int Count => _layers.Count;

        public LayerDefinition this[int index] => _layers[index];

        public int HeartTierIndex => _heartTierIndex;

        /// <summary>Local position of the pocket's center, relative to the mountain's base center.</summary>
        public Vector3 PocketCenter => new Vector3(0f, _pocketHeight, 0f);

        public float PocketRadius => _pocketRadius;

        public LayerLayout CreateLayout()
        {
            // Every layer but the last is a shell by depth; the last is the pocket.
            float[] bandStarts = new float[Mathf.Max(1, _layers.Count - 1)];
            for (int i = 0; i < bandStarts.Length && i < _layers.Count; i++)
            {
                bandStarts[i] = _layers[i] != null ? _layers[i].StartDepth : 0f;
            }

            float pocketRadius = _layers.Count > 1 ? _pocketRadius : 0f;
            return new LayerLayout(bandStarts, PocketCenter, pocketRadius);
        }

        public LayerSpawnTable CreateSpawnTable(int tierCount)
        {
            float[] rates = new float[_layers.Count];
            float[][] weights = new float[_layers.Count][];
            for (int i = 0; i < _layers.Count; i++)
            {
                LayerDefinition layer = _layers[i];
                rates[i] = layer != null ? layer.EmbersPerCubicMeter : 0f;
                weights[i] = layer != null ? layer.TierWeights(tierCount) : new float[tierCount];
            }

            return new LayerSpawnTable(rates, weights);
        }

        public Color[] CreateTints()
        {
            Color[] tints = new Color[_layers.Count];
            for (int i = 0; i < tints.Length; i++)
            {
                tints[i] = _layers[i] != null ? _layers[i].Tint : Color.white;
            }

            return tints;
        }

        public Color[] CreateDustColors()
        {
            Color[] colors = new Color[_layers.Count];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = _layers[i] != null ? _layers[i].DustColor : Color.white;
            }

            return colors;
        }

        public string NameOf(int layer)
        {
            return layer >= 0 && layer < _layers.Count && _layers[layer] != null ? _layers[layer].DisplayName : $"Layer {layer + 1}";
        }
    }
}
