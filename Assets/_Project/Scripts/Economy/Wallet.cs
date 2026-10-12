using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>
    /// Owns the player's <see cref="Economy.Currencies"/> so other components can reach them through the Inspector.
    /// </summary>
    public sealed class Wallet : MonoBehaviour
    {
        public Currencies Currencies { get; } = new Currencies();
    }
}
