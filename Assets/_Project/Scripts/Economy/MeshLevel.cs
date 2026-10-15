using System;
using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>One sieve mesh: how likely a small ember is to slip through it while shaking.</summary>
    [Serializable]
    public struct MeshLevel
    {
        [SerializeField] private string _name;
        [Tooltip("Chance (0..1) that each small ember (e.g. a Spark) in a load slips through while it is shaken out.")]
        [SerializeField, Range(0f, 1f)] private float _slipChance;

        public MeshLevel(string name, float slipChance)
        {
            _name = name;
            _slipChance = slipChance;
        }

        public string Name => _name;
        public float SlipChance => _slipChance;
    }
}
