using System;
using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>One level of a <see cref="TableUpgrade"/>: its name, the Glow to reach it, the value it gives and a line for the shop.</summary>
    [Serializable]
    public struct TableLevel
    {
        [SerializeField] private string _name;
        [Tooltip("Glow to reach this level from the one before. Ignored for level 0.")]
        [SerializeField, Min(0)] private int _price;
        [Tooltip("What this level gives; the upgrade's tooltip says what the number means.")]
        [SerializeField] private float _value;
        [Tooltip("One line for the shop, e.g. \"Holds 15 embers\".")]
        [SerializeField] private string _description;

        public TableLevel(string name, int price, float value, string description)
        {
            _name = name;
            _price = price;
            _value = value;
            _description = description;
        }

        public string Name => _name;
        public int Price => _price;
        public float Value => _value;
        public string Description => _description;
    }
}
