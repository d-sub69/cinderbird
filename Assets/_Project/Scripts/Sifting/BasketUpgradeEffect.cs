using Cinderbird.Economy;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Thin adapter: gives the basket the capacity and size of the basket upgrade's current level. Applied at start
    /// and whenever the level changes (bought, or loaded from a save, before the saved embers go back in). Lives on an
    /// always-active object: the basket itself is inactive while put away.
    /// </summary>
    public sealed class BasketUpgradeEffect : MonoBehaviour
    {
        [SerializeField] private Upgrades _upgrades;
        [SerializeField] private BasketUpgrade _upgrade;
        [SerializeField] private Basket _basket;
        [SerializeField] private BasketView _view;

        private void Awake()
        {
            if (_upgrades == null || _upgrade == null || _basket == null || _view == null)
            {
                Debug.LogError($"{nameof(BasketUpgradeEffect)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _upgrades.Levels.Changed += OnLevelChanged;
        }

        private void OnDisable()
        {
            _upgrades.Levels.Changed -= OnLevelChanged;
        }

        private void Start()
        {
            Apply();
        }

        private void OnLevelChanged(string id)
        {
            if (id == _upgrade.Id)
            {
                Apply();
            }
        }

        private void Apply()
        {
            BasketLevel level = _upgrade.LevelAt(_upgrades.LevelOf(_upgrade));
            _basket.SetCapacity(level.Capacity);
            _view.SetSize(level.VisualScale, level.Capacity);
        }
    }
}
