using System;
using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>One sieve the player can own: how big a scoop it takes, how many embers it holds, how big it looks.</summary>
    [Serializable]
    public struct SieveTier
    {
        [SerializeField] private string _name;
        [Tooltip("Radius of the sphere a scoop digs, in meters.")]
        [SerializeField, Min(0.05f)] private float _scoopRadius;
        [Tooltip("Scooping stops once the sieve holds this many embers.")]
        [SerializeField, Min(1)] private int _emberCapacity;
        [Tooltip("Size of the sieve in the hands, relative to the first one.")]
        [SerializeField, Min(0.1f)] private float _visualScale;

        public SieveTier(string name, float scoopRadius, int emberCapacity, float visualScale)
        {
            _name = name;
            _scoopRadius = scoopRadius;
            _emberCapacity = emberCapacity;
            _visualScale = visualScale;
        }

        public string Name => _name;
        public float ScoopRadius => _scoopRadius;
        public int EmberCapacity => _emberCapacity;
        public float VisualScale => _visualScale;
    }
}
