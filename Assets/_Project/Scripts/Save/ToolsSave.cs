using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>The inventory (tool ids per slot, empty string for an empty slot) and the tools set down in the world.</summary>
    [Serializable]
    public sealed class ToolsSave
    {
        [SerializeField] private List<string> _slots = new List<string>();
        [SerializeField] private int _activeSlot;
        [SerializeField] private List<PlacedToolSave> _placed = new List<PlacedToolSave>();

        public List<string> Slots => _slots;
        public int ActiveSlot { get => _activeSlot; set => _activeSlot = value; }
        public List<PlacedToolSave> Placed => _placed;
    }
}
