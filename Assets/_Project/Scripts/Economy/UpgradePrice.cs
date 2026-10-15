using System;

namespace Cinderbird.Economy
{
    /// <summary>The design's upgrade price curve: base × growth^level, rounded to whole Glow.</summary>
    public static class UpgradePrice
    {
        /// <param name="level">The level being bought from (0 = the first purchase).</param>
        public static int At(int baseCost, float growth, int level)
        {
            if (level < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "Levels start at 0.");
            }

            // 1.15f is really 1.1499999...; read it as the 1.15 the designer typed, so 30 → 35, not 34.
            double exactGrowth = Math.Round((double)Math.Max(1f, growth), 4);
            double price = Math.Max(0, baseCost) * Math.Pow(exactGrowth, level);
            return price >= int.MaxValue ? int.MaxValue : (int)Math.Round(price, MidpointRounding.AwayFromZero);
        }
    }
}
