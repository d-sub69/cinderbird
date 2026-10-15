using System;
using UnityEngine;

namespace Cinderbird.Offerings
{
    /// <summary>Every offering in the game, in shrine order, and the sets they form.</summary>
    [CreateAssetMenu(fileName = "OfferingCatalog", menuName = "Cinderbird/Offerings/Catalog")]
    public sealed class OfferingCatalog : ScriptableObject
    {
        [SerializeField] private OfferingDefinition[] _offerings = new OfferingDefinition[0];
        [SerializeField] private OfferingSet[] _sets = new OfferingSet[0];

        public int Count => _offerings.Length;
        public OfferingDefinition this[int index] => _offerings[index];
        public OfferingSet[] Sets => _sets;

        public int IndexOf(OfferingDefinition offering)
        {
            return Array.IndexOf(_offerings, offering);
        }

        /// <summary>The index of the offering with this id, or -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < _offerings.Length; i++)
            {
                if (_offerings[i] != null && _offerings[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
