using System;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>Where one offering is (lying on the pile, in the collection, on the shelf, sold), by its stable id.</summary>
    [Serializable]
    public sealed class OfferingStateSave
    {
        [SerializeField] private string _id = string.Empty;
        [Tooltip("An OfferingState as a number: 1 in the collection, 2 on the shelf, 3 sold, 4 lying on the pile (0, buried, isn't saved).")]
        [SerializeField] private int _state;
        [Tooltip("Where it lies on the pile (terrain-local), while it does.")]
        [SerializeField] private Vector3 _position;

        public OfferingStateSave()
        {
        }

        public OfferingStateSave(string id, int state, Vector3 position = default)
        {
            _id = id;
            _state = state;
            _position = position;
        }

        public string Id => _id;
        public int State => _state;
        public Vector3 Position => _position;
    }
}
