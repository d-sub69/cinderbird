using System;
using UnityEngine;

namespace Cinderbird.Save
{
    [Serializable]
    public sealed class CurrencySave
    {
        [SerializeField] private int _glow;
        [SerializeField] private int _heat;

        public int Glow { get => _glow; set => _glow = value; }
        public int Heat { get => _heat; set => _heat = value; }
    }
}
