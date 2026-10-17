using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>
    /// Something that comes with a Heat level of the nest (a power). Upgrades can point at it to go on sale together with
    /// it, so the level is set in one place only.
    /// </summary>
    public abstract class HeatGate : ScriptableObject
    {
        /// <summary>The nest's Heat level it comes with.</summary>
        public abstract int RequiredHeatLevel { get; }
    }
}
