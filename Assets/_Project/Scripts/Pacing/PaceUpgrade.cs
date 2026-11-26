using System;

namespace Cinderbird.Pacing
{
    /// <summary>One upgrade line as the pacing simulator sees it: what each level costs and what it changes.</summary>
    public sealed class PaceUpgrade
    {
        private readonly int[] _prices;
        private readonly float[] _values;
        private readonly int[] _capacities;
        private readonly float[] _scales;

        /// <param name="prices">Glow to go from level i to i + 1, for i = 0 .. max − 1.</param>
        /// <param name="values">The effect's value at each level 0 .. max (see <see cref="PaceEffect"/>).</param>
        /// <param name="capacities">Optional second value per level (the sieve's ember capacity).</param>
        /// <param name="requiresId">Optional: another line that needs level 1 first (e.g. the helper's hire).</param>
        /// <param name="heatLevel">Heat level before it is offered.</param>
        /// <param name="scales">Optional third value per level (the sieve's shake speed against the first sieve).</param>
        public PaceUpgrade(string id, string name, PaceEffect effect, int[] prices, float[] values, int[] capacities = null,
            string requiresId = null, int heatLevel = 0, float[] scales = null)
        {
            Id = id ?? string.Empty;
            Name = name ?? Id;
            Effect = effect;
            _prices = prices ?? Array.Empty<int>();
            _values = values ?? Array.Empty<float>();
            _capacities = capacities ?? Array.Empty<int>();
            _scales = scales ?? Array.Empty<float>();
            RequiresId = requiresId ?? string.Empty;
            HeatLevel = heatLevel;
        }

        public string Id { get; }

        public string Name { get; }

        public PaceEffect Effect { get; }

        public string RequiresId { get; }

        public int HeatLevel { get; }

        /// <summary>The highest level there is (0 when nothing can be bought).</summary>
        public int MaxLevel => _prices.Length;

        /// <summary>Glow to go from <paramref name="level"/> to the next.</summary>
        public int PriceFrom(int level)
        {
            return level >= 0 && level < _prices.Length ? _prices[level] : int.MaxValue;
        }

        public float ValueAt(int level)
        {
            return _values.Length == 0 ? 0f : _values[Math.Max(0, Math.Min(level, _values.Length - 1))];
        }

        public float ScaleAt(int level, float fallback)
        {
            return _scales.Length == 0 ? fallback : _scales[Math.Max(0, Math.Min(level, _scales.Length - 1))];
        }

        public int CapacityAt(int level)
        {
            return _capacities.Length == 0 ? 0 : _capacities[Math.Max(0, Math.Min(level, _capacities.Length - 1))];
        }
    }
}
