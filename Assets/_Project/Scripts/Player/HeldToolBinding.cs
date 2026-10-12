using System;
using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>Pairs a tool definition with the object that shows it in the player's hands.</summary>
    [Serializable]
    public sealed class HeldToolBinding
    {
        [SerializeField] private ToolDefinition _tool;
        [Tooltip("The tool's root under the camera's Hands; shown only while this tool is active.")]
        [SerializeField] private GameObject _heldObject;

        public ToolDefinition Tool => _tool;
        public GameObject HeldObject => _heldObject;
    }
}
