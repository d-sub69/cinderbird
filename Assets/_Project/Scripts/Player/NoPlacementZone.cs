using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// Marks an area where tools can't be set down (the nest; later the shop stall, the shrine).
    /// The area is this object's collider(s); a trigger is enough.
    /// </summary>
    public sealed class NoPlacementZone : MonoBehaviour
    {
        [Tooltip("For messages, e.g. \"Can't place the basket in the nest\".")]
        [SerializeField] private string _areaName = "the nest";

        public string AreaName => _areaName;
    }
}
