using UnityEngine;

namespace Cinderbird.Offerings
{
    /// <summary>The shrine's rules that may be tuned or upgraded.</summary>
    [CreateAssetMenu(fileName = "ShrineSettings", menuName = "Cinderbird/Offerings/Shrine Settings")]
    public sealed class ShrineSettings : ScriptableObject
    {
        [Tooltip("How many complete shelf sets give their bonus at once. One at the start; an upgrade can add more later.")]
        [SerializeField, Min(0)] private int _activeSetSlots = 1;

        public int ActiveSetSlots => _activeSetSlots;
    }
}
