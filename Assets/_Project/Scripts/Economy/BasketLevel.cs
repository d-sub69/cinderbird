using System;
using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>One basket: how many embers it carries and how big it looks.</summary>
    [Serializable]
    public struct BasketLevel
    {
        [SerializeField] private string _name;
        [SerializeField, Min(1)] private int _capacity;
        [Tooltip("Size relative to the first basket; about the square root of the capacity ratio keeps embers in one layer.")]
        [SerializeField, Min(0.1f)] private float _visualScale;

        public BasketLevel(string name, int capacity, float visualScale)
        {
            _name = name;
            _capacity = capacity;
            _visualScale = visualScale;
        }

        public string Name => _name;
        public int Capacity => _capacity;
        public float VisualScale => _visualScale;
    }
}
