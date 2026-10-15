using System;

namespace Cinderbird.Economy
{
    /// <summary>The shop's rules: what the next level of an upgrade costs, and buying it with Glow.</summary>
    public sealed class Shop
    {
        private readonly Currencies _currencies;
        private readonly UpgradeLevels _levels;

        public Shop(Currencies currencies, UpgradeLevels levels)
        {
            _currencies = currencies ?? throw new ArgumentNullException(nameof(currencies));
            _levels = levels ?? throw new ArgumentNullException(nameof(levels));
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
            return price >= 0 && _currencies.Glow >= price;
        }

        public Purchase TryBuy(IUpgrade upgrade)
        {
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
