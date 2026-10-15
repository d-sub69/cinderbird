using System;
using System.Collections.Generic;

namespace Cinderbird.Economy
{
    /// <summary>The level the player has reached in each upgrade, by upgrade id; 0 for anything never bought.</summary>
    public sealed class UpgradeLevels
    {
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();

        /// <summary>Fired with the id of an upgrade whose level changed (bought, or set from a save).</summary>
        public event Action<string> Changed;

        public int LevelOf(string id)
        {
            return id != null && _levels.TryGetValue(id, out int level) ? level : 0;
        }

        public void SetLevel(string id, int level)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("An upgrade needs an id.", nameof(id));
            }

            level = Math.Max(0, level);
            if (LevelOf(id) == level)
            {
                return;
            }

            if (level == 0)
            {
                _levels.Remove(id);
            }
            else
            {
                _levels[id] = level;
            }

            Changed?.Invoke(id);
        }

        /// <summary>Every upgrade above level 0, for saving.</summary>
        public void CopyTo(List<KeyValuePair<string, int>> output)
        {
            output.Clear();
            foreach (KeyValuePair<string, int> level in _levels)
            {
                output.Add(level);
            }
        }

        /// <summary>Replaces all levels with saved ones; upgrades not listed go back to 0.</summary>
        public void Restore(IReadOnlyList<KeyValuePair<string, int>> saved)
        {
            List<string> previous = new List<string>(_levels.Keys);
            _levels.Clear();
            for (int i = 0; i < saved.Count; i++)
            {
                if (!string.IsNullOrEmpty(saved[i].Key) && saved[i].Value > 0)
                {
                    _levels[saved[i].Key] = saved[i].Value;
                }
            }

            foreach (string id in previous)
            {
                if (!_levels.ContainsKey(id))
                {
                    Changed?.Invoke(id);
                }
            }

            foreach (string id in _levels.Keys)
            {
                Changed?.Invoke(id);
            }
        }
    }
}
