using System;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Plain-data ember spawn rules per ash layer: how many embers per cubic meter, and how likely each tier is.
    /// Built from the layer assets, so the spawner can be tested without ScriptableObjects.
    /// </summary>
    public sealed class LayerSpawnTable
    {
        private readonly float[] _embersPerCubicMeter;
        private readonly float[][] _tierWeights;
        private readonly float[] _totalWeights;

        /// <param name="embersPerCubicMeter">Per layer.</param>
        /// <param name="tierWeights">Per layer, one weight per tier in catalog order.</param>
        public LayerSpawnTable(float[] embersPerCubicMeter, float[][] tierWeights)
        {
            if (embersPerCubicMeter == null || tierWeights == null || embersPerCubicMeter.Length != tierWeights.Length)
            {
                throw new ArgumentException("Need one density and one weight list per layer.");
            }

            _embersPerCubicMeter = (float[])embersPerCubicMeter.Clone();
            _tierWeights = new float[tierWeights.Length][];
            _totalWeights = new float[tierWeights.Length];
            for (int layer = 0; layer < tierWeights.Length; layer++)
            {
                _tierWeights[layer] = tierWeights[layer] != null ? (float[])tierWeights[layer].Clone() : Array.Empty<float>();
                foreach (float weight in _tierWeights[layer])
                {
                    _totalWeights[layer] += Math.Max(0f, weight);
                }

                // A layer with no tiers to pick can't hold embers, whatever its density says.
                if (_totalWeights[layer] <= 0f)
                {
                    _embersPerCubicMeter[layer] = 0f;
                }

                MaxEmbersPerCubicMeter = Math.Max(MaxEmbersPerCubicMeter, _embersPerCubicMeter[layer]);
            }
        }

        public int LayerCount => _embersPerCubicMeter.Length;

        /// <summary>The densest layer's rate; the spawner samples at this rate and thins out per layer.</summary>
        public float MaxEmbersPerCubicMeter { get; }

        public float EmbersPerCubicMeter(int layer)
        {
            return layer >= 0 && layer < _embersPerCubicMeter.Length ? _embersPerCubicMeter[layer] : 0f;
        }

        public float[] TierWeights(int layer)
        {
            return _tierWeights[layer];
        }

        public float TotalWeight(int layer)
        {
            return layer >= 0 && layer < _totalWeights.Length ? _totalWeights[layer] : 0f;
        }
    }
}
