using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>
    /// An upgrade the shop sells, as data: its id, name and price curve (base × growth^level). Each kind of upgrade
    /// adds its own per-level values (<see cref="SieveUpgrade"/>, <see cref="MeshUpgrade"/>, <see cref="BasketUpgrade"/>).
    /// </summary>
    public abstract class UpgradeDefinition : ScriptableObject, IUpgrade
    {
        [Tooltip("Stable id used by saves. Never change it once players have saves.")]
        [SerializeField] private string _id = "upgrade";
        [SerializeField] private string _displayName = "Upgrade";
        [Tooltip("Glow for the first purchase.")]
        [SerializeField, Min(0)] private int _baseCost = 30;
        [Tooltip("Each level costs this much more than the one before (the design's default is 1.15).")]
        [SerializeField, Min(1f)] private float _costGrowth = 1.15f;

        public string Id => _id;
        public string DisplayName => _displayName;

        /// <summary>Levels 0 .. MaxLevel; 0 is what the player starts with.</summary>
        public int MaxLevel => Mathf.Max(0, LevelCount - 1);

        public int PriceAt(int level)
        {
            return UpgradePrice.At(_baseCost, _costGrowth, level);
        }

        /// <summary>Name of what the player has at a level (e.g. "Wooden sieve").</summary>
        public abstract string LevelName(int level);

        /// <summary>One line on what a level gives, for the shop.</summary>
        public abstract string Describe(int level);

        protected abstract int LevelCount { get; }

        protected static int Clamp(int level, int count)
        {
            return Mathf.Clamp(level, 0, Mathf.Max(0, count - 1));
        }
    }
}
