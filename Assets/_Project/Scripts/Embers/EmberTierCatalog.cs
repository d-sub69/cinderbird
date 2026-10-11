using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// The ordered list of ember tiers. <see cref="Ember.TierIndex"/> indexes into it, so don't reorder
    /// tiers once save data exists.
    /// </summary>
    [CreateAssetMenu(fileName = "EmberTierCatalog", menuName = "Cinderbird/Ember Tier Catalog")]
    public sealed class EmberTierCatalog : ScriptableObject
    {
        [SerializeField] private List<EmberTier> _tiers = new List<EmberTier>();

        public int Count => _tiers.Count;

        public EmberTier this[int index] => _tiers[index];

        public void CopySpawnWeights(List<float> output)
        {
            output.Clear();
            for (int i = 0; i < _tiers.Count; i++)
            {
                output.Add(_tiers[i] != null ? _tiers[i].SpawnWeight : 0f);
            }
        }
    }
}
