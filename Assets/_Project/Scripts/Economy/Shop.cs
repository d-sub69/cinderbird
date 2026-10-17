using System;

namespace Cinderbird.Economy
{
    /// <summary>The shop's rules: what the next level of an upgrade costs, and buying it with Glow.</summary>
    public sealed class Shop
    {
        private readonly Currencies _currencies;
        private readonly UpgradeLevels _levels;

        /// <summary>Fired when what is on sale may have changed without a purchase (the nest reached a Heat level).</summary>
        public event Action AvailabilityChanged;

        /// <summary>The nest's Heat level: some upgrades go on sale with one.</summary>
        public int HeatLevel { get; private set; }

        public Shop(Currencies currencies, UpgradeLevels levels)
        {
            _currencies = currencies ?? throw new ArgumentNullException(nameof(currencies));
            _levels = levels ?? throw new ArgumentNullException(nameof(levels));
        }

        /// <summary>Whether it is on sale: whatever it requires has been bought, and the nest is warm enough for it.</summary>
        public bool IsAvailable(IUpgrade upgrade)
        {
            return (string.IsNullOrEmpty(upgrade.RequiresId) || _levels.LevelOf(upgrade.RequiresId) > 0) && HeatLevel >= upgrade.RequiredHeatLevel;
        }

        public void SetHeatLevel(int level)
        {
            if (level == HeatLevel)
            {
                return;
            }

            HeatLevel = level;
            AvailabilityChanged?.Invoke();
        }

        public bool IsMaxed(IUpgrade upgrade)
        {
            return _levels.LevelOf(upgrade.Id) >= upgrade.MaxLevel;
        }

        /// <summary>Glow for the next level; -1 once the upgrade is at its top level.</summary>
        public int NextPrice(IUpgrade upgrade)
        {
            return IsMaxed(upgrade) ? -1 : upgrade.PriceAt(_levels.LevelOf(upgrade.Id));
        }

        public bool CanAfford(IUpgrade upgrade)
        {
            int price = NextPrice(upgrade);
            return price >= 0 && IsAvailable(upgrade) && _currencies.Glow >= price;
        }

        public Purchase TryBuy(IUpgrade upgrade)
        {
            if (!IsAvailable(upgrade))
            {
                return Purchase.Locked;
            }

            if (IsMaxed(upgrade))
            {
                return Purchase.AlreadyMaxed;
            }

            int level = _levels.LevelOf(upgrade.Id);
            if (!_currencies.TrySpend(upgrade.PriceAt(level)))
            {
                return Purchase.NotEnoughGlow;
            }

            _levels.SetLevel(upgrade.Id, level + 1);
            return Purchase.Bought;
        }
    }
}
