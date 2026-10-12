using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>A tool the player can carry in the inventory: the sieve, the basket, later the pick and gloves.</summary>
    [CreateAssetMenu(fileName = "Tool", menuName = "Cinderbird/Tool Definition")]
    public sealed class ToolDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "Tool";

        public string DisplayName => _displayName;
    }
}
