using UnityEngine;

namespace Cinderbird.Player
{
    [CreateAssetMenu(fileName = "ToolBeltSettings", menuName = "Cinderbird/Tool Belt Settings")]
    public sealed class ToolBeltSettings : ScriptableObject
    {
        [Tooltip("Inventory slots for tools.")]
        [SerializeField, Min(1)] private int _capacity = 5;

        [Header("Switching tools")]
        [Tooltip("Seconds for the whole swap: lower the old tool, raise the new one.")]
        [SerializeField, Min(0f)] private float _swapDuration = 0.25f;
        [Tooltip("How far the hands drop out of view during a swap, in meters.")]
        [SerializeField, Min(0f)] private float _lowerDistance = 0.35f;

        [Header("Interaction")]
        [Tooltip("How far you can look at something (nest, placed basket) and still use it, in meters.")]
        [SerializeField, Min(0.1f)] private float _interactReach = 3f;

        public int Capacity => _capacity;
        public float SwapDuration => _swapDuration;
        public float LowerDistance => _lowerDistance;
        public float InteractReach => _interactReach;
    }
}
