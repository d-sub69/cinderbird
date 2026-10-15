using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>Bigger baskets: more embers per trip to the nest.</summary>
    [CreateAssetMenu(fileName = "Upgrade_Basket", menuName = "Cinderbird/Upgrades/Basket")]
    public sealed class BasketUpgrade : UpgradeDefinition
    {
        [Tooltip("Level 0 first: the basket the player starts with.")]
        [SerializeField] private BasketLevel[] _levels =
        {
            new BasketLevel("Basket", 25, 1f),
        };

        public BasketLevel LevelAt(int level)
        {
            return _levels[Clamp(level, _levels.Length)];
        }

        public override string LevelName(int level)
        {
            return LevelAt(level).Name;
        }

        public override string Describe(int level)
        {
            return $"Holds {LevelAt(level).Capacity} embers";
        }

        protected override int LevelCount => _levels.Length;
    }
}
