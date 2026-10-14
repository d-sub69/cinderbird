using System;
using Cinderbird.Embers;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// How many embers each ash layer had when generated and how many are still on the mountain (buried or lying
    /// loose). Their ratio is how cleared a layer is, which opens the next one.
    /// </summary>
    public sealed class LayerEmberCounts
    {
        private readonly int[] _initial;
        private readonly int[] _onMountain;

        public LayerEmberCounts(int layerCount)
        {
            _initial = new int[layerCount];
            _onMountain = new int[layerCount];
        }

        /// <summary>Embers still on the mountain, all layers together.</summary>
        public int Total { get; private set; }

        /// <summary>Share of a layer's embers already taken off the mountain (0..1); 1 for a layer that had none.</summary>
        public float ClearedShare(int layer)
        {
            if (layer < 0 || layer >= _initial.Length || _initial[layer] == 0)
            {
                return 1f;
            }

            return 1f - _onMountain[layer] / (float)_initial[layer];
        }

        public int InitialIn(int layer)
        {
            return layer >= 0 && layer < _initial.Length ? _initial[layer] : 0;
        }

        public int OnMountainIn(int layer)
        {
            return layer >= 0 && layer < _onMountain.Length ? _onMountain[layer] : 0;
        }

        /// <summary>An ember left the mountain (collected).</summary>
        public void Taken(Ember ember)
        {
            Total--;
            if (ember.Layer < _onMountain.Length)
            {
                _onMountain[ember.Layer]--;
            }
        }

        /// <summary>An ember is (back) on the mountain.</summary>
        public void Returned(Ember ember)
        {
            Total++;
            if (ember.Layer < _onMountain.Length)
            {
                _onMountain[ember.Layer]++;
            }
        }

        /// <summary>An ember that is part of the mountain as generated: counts toward the initial number too.</summary>
        public void AddGenerated(Ember ember)
        {
            Returned(ember);
            if (ember.Layer < _initial.Length)
            {
                _initial[ember.Layer]++;
            }
        }

        /// <summary>Starts a recount: nothing on the mountain until <see cref="Returned"/> is called for each ember.</summary>
        public void ClearOnMountain()
        {
            Total = 0;
            Array.Clear(_onMountain, 0, _onMountain.Length);
        }

        /// <summary>After generating: what is on the mountain now is what each layer started with.</summary>
        public void SetInitialToCurrent()
        {
            Array.Copy(_onMountain, _initial, _onMountain.Length);
        }
    }
}
