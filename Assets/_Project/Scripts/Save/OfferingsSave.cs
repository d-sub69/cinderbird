using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>Every offering uncovered so far and where it is, and which shelf sets give their bonus; the rest are still in the ash.</summary>
    [Serializable]
    public sealed class OfferingsSave
    {
        [SerializeField] private List<OfferingStateSave> _found = new List<OfferingStateSave>();
        [Tooltip("Ids of the complete sets giving their bonus.")]
        [SerializeField] private List<string> _activeSets = new List<string>();

        public List<OfferingStateSave> Found => _found;
        public List<string> ActiveSets => _activeSets;
    }
}
