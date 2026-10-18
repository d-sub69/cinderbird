using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>
    /// Owns the player's <see cref="Economy.Currencies"/> and feeding <see cref="FeedBonuses"/> so other components can
    /// reach them through the Inspector.
    /// </summary>
    public sealed class Wallet : MonoBehaviour
    {
        public Currencies Currencies { get; } = new Currencies();

        /// <summary>Extra Glow and Heat from feeding (e.g. shelf sets).</summary>
        public FeedBonuses Bonuses { get; } = new FeedBonuses();
    }
}
