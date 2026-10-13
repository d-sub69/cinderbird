using System;
using System.Collections.Generic;

namespace Cinderbird.Economy
{
    /// <summary>
    /// The nest's Heat levels: how much Heat each level needs, and which level a Heat total reaches. Heat is never
    /// spent, so a level once reached stays reached. Plain C# so the rules can be tested.
    /// </summary>
    public sealed class HeatLevels
    {
        private readonly int[] _thresholds;

        /// <param name="thresholds">Heat needed for level 1, 2, …; each more than the one before.</param>
        public HeatLevels(IReadOnlyList<int> thresholds)
        {
            if (thresholds == null || thresholds.Count == 0)
            {
                throw new ArgumentException("There must be at least one Heat level.", nameof(thresholds));
            }

            _thresholds = new int[thresholds.Count];
            for (int i = 0; i < thresholds.Count; i++)
            {
                if (thresholds[i] <= 0 || (i > 0 && thresholds[i] <= thresholds[i - 1]))
                {
                    throw new ArgumentException("Heat levels must need more Heat each, starting above 0.", nameof(thresholds));
                }

                _thresholds[i] = thresholds[i];
            }
        }

        public int MaxLevel => _thresholds.Length;

        /// <returns>0 before level 1, up to <see cref="MaxLevel"/>.</returns>
        public int LevelFor(int heat)
        {
            int level = 0;
            while (level < _thresholds.Length && heat >= _thresholds[level])
            {
                level++;
            }

            return level;
        }

        /// <summary>Heat needed to reach a level; 0 for level 0, the top threshold beyond the last level.</summary>
        public int HeatFor(int level)
        {
            if (level <= 0)
            {
                return 0;
            }

            return _thresholds[Math.Min(level, _thresholds.Length) - 1];
        }

        /// <summary>How far a Heat total is from its level to the next (0..1); 1 at the top level.</summary>
        public float ProgressToNext(int heat)
        {
            int level = LevelFor(heat);
            if (level >= _thresholds.Length)
            {
                return 1f;
            }

            int from = HeatFor(level);
            int to = _thresholds[level];
            return Math.Min(1f, Math.Max(0f, (heat - from) / (float)(to - from)));
        }
    }
}
