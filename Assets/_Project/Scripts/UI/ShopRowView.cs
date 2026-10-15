using System;
using Cinderbird.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>One line of the shop: an upgrade, what the player has, what the next level gives, and its price.</summary>
    public sealed class ShopRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _details;
        [SerializeField] private Button _buyButton;
        [SerializeField] private TMP_Text _buyLabel;

        private UpgradeDefinition _upgrade;
        private Upgrades _upgrades;

        public void Bind(UpgradeDefinition upgrade, Upgrades upgrades, Action<UpgradeDefinition> buy)
        {
            _upgrade = upgrade;
            _upgrades = upgrades;
            _buyButton.onClick.RemoveAllListeners();
            _buyButton.onClick.AddListener(() => buy(_upgrade));
        }

        public void Refresh()
        {
            if (_upgrade == null)
            {
                return;
            }

            Shop shop = _upgrades.Shop;
            int level = _upgrades.LevelOf(_upgrade);
            _name.text = $"{_upgrade.DisplayName}: {_upgrade.LevelName(level)}";
            if (shop.IsMaxed(_upgrade))
            {
                _details.text = $"{_upgrade.Describe(level)}. The best there is.";
                _buyLabel.text = "Owned";
                _buyButton.interactable = false;
                return;
            }

            _details.text = $"Next: {_upgrade.LevelName(level + 1)}. {_upgrade.Describe(level + 1)}";
            _buyLabel.text = $"{shop.NextPrice(_upgrade):N0} Glow";
            _buyButton.interactable = shop.CanAfford(_upgrade);
        }
    }
}
