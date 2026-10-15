using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>
    /// Owns the player's <see cref="UpgradeLevels"/> and the <see cref="Economy.Shop"/> that sells them, so tools,
    /// the shop stall and saves reach them through the Inspector. The catalog is everything on sale, in shop order.
    /// </summary>
    public sealed class Upgrades : MonoBehaviour
    {
        [SerializeField] private Wallet _wallet;
        [Tooltip("Everything the shop sells, in the order it lists them.")]
        [SerializeField] private UpgradeDefinition[] _catalog = new UpgradeDefinition[0];

        private Shop _shop;

        public UpgradeLevels Levels { get; } = new UpgradeLevels();

        public UpgradeDefinition[] Catalog => _catalog;

        /// <summary>Created on first access: other objects' OnEnable can run before this Awake.</summary>
        public Shop Shop
        {
            get
            {
                if (_shop == null)
                {
                    if (_wallet == null)
                    {
                        Debug.LogError($"{nameof(Upgrades)} on '{name}' has no wallet.", this);
                        return null;
                    }

                    _shop = new Shop(_wallet.Currencies, Levels);
                }

                return _shop;
            }
        }

        public Wallet Wallet => _wallet;

        public int LevelOf(UpgradeDefinition upgrade)
        {
            return upgrade != null ? Mathf.Min(Levels.LevelOf(upgrade.Id), upgrade.MaxLevel) : 0;
        }

        /// <summary>The catalog entry with this id, or null (e.g. an upgrade removed since the save was made).</summary>
        public UpgradeDefinition Find(string id)
        {
            foreach (UpgradeDefinition upgrade in _catalog)
            {
                if (upgrade != null && upgrade.Id == id)
                {
                    return upgrade;
                }
            }

            return null;
        }
    }
}
