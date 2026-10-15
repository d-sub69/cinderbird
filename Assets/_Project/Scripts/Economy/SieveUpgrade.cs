using System.Globalization;
using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>The sieves, from the one the player starts with to the best: each level is a bigger sieve.</summary>
    [CreateAssetMenu(fileName = "Upgrade_Sieve", menuName = "Cinderbird/Upgrades/Sieve")]
    public sealed class SieveUpgrade : UpgradeDefinition
    {
        [Tooltip("Level 0 first: the sieve the player starts with.")]
        [SerializeField] private SieveTier[] _tiers =
        {
            new SieveTier("Hand sieve", 0.4f, 16, 1f),
        };

        public SieveTier TierAt(int level)
        {
            return _tiers[Clamp(level, _tiers.Length)];
        }

        public override string LevelName(int level)
        {
            return TierAt(level).Name;
        }

        public override string Describe(int level)
        {
            SieveTier tier = TierAt(level);
            return string.Format(CultureInfo.InvariantCulture, "Scoops {0:0} cm wide, holds {1} embers",
                tier.ScoopRadius * 200f, tier.EmberCapacity);
        }

        protected override int LevelCount => _tiers.Length;
    }
}
