using System;
using System.Collections.Generic;

namespace Cinderbird.Economy
{
    /// <summary>
    /// Extra Glow and Heat from feeding the nest, in percent, from any number of named sources (e.g. shelf sets).
    /// Fractions are carried over between feedings, so +10% on single sparks isn't lost
    /// to rounding.
    /// </summary>
    public sealed class FeedBonuses
    {
        private readonly Dictionary<string, float> _glowBySource = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _heatBySource = new Dictionary<string, float>();
        private float _glowCarry;
        private float _heatCarry;

        public event Action Changed;

        /// <summary>All Glow bonuses together, in percent.</summary>
        public float GlowPercent { get; private set; }

        /// <summary>All Heat bonuses together, in percent.</summary>
        public float HeatPercent { get; private set; }

        /// <summary>Sets what one source adds; 0 and 0 removes it.</summary>
        public void Set(string source, float glowPercent, float heatPercent)
        {
            Store(_glowBySource, source, glowPercent);
            Store(_heatBySource, source, heatPercent);
            GlowPercent = Sum(_glowBySource);
            HeatPercent = Sum(_heatBySource);
            Changed?.Invoke();
        }

        /// <summary>What a feeding earns with the bonuses on top.</summary>
        public void Apply(ref int glow, ref int heat)
        {
            glow = AddPercent(glow, GlowPercent, ref _glowCarry);
            heat = AddPercent(heat, HeatPercent, ref _heatCarry);
        }

        private static int AddPercent(int amount, float percent, ref float carry)
        {
            if (amount <= 0 || percent <= 0f)
            {
                return amount;
            }

            float extra = amount * percent / 100f + carry;
            int whole = (int)Math.Floor(extra);
            carry = extra - whole;
            return amount + whole;
        }

        private static void Store(Dictionary<string, float> bySource, string source, float percent)
        {
            if (percent == 0f)
            {
                bySource.Remove(source);
            }
            else
            {
                bySource[source] = percent;
            }
        }

        private static float Sum(Dictionary<string, float> bySource)
        {
            float sum = 0f;
            foreach (float percent in bySource.Values)
            {
                sum += percent;
            }

            return sum;
        }
    }
}
