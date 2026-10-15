using System.Collections.Generic;

namespace Cinderbird.Offerings
{
    /// <summary>
    /// Which complete shelf sets give their bonus: at most <see cref="Slots"/> of them (one at the start; upgrades add
    /// more). A set that becomes complete takes a free slot by itself; with no slot free, the player chooses which set
    /// is active. A set that loses a piece stops being active. Sets are known by index.
    /// </summary>
    public sealed class ActiveSets
    {
        private readonly List<int> _active = new List<int>();

        public ActiveSets(int slots)
        {
            Slots = slots;
        }

        /// <summary>How many sets can give their bonus at once.</summary>
        public int Slots { get; set; }

        public IReadOnlyList<int> Active => _active;

        public bool IsActive(int set)
        {
            return _active.Contains(set);
        }

        /// <summary>After the shelf changed: drops sets no longer complete, and lets a set that just became complete take a free slot.</summary>
        /// <param name="complete">Per set, whether all its offerings are on the shelf.</param>
        /// <param name="justCompleted">A set that became complete with this change, or -1.</param>
        public void Update(IReadOnlyList<bool> complete, int justCompleted)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!IsComplete(complete, _active[i]))
                {
                    _active.RemoveAt(i);
                }
            }

            if (justCompleted >= 0 && !_active.Contains(justCompleted) && _active.Count < Slots && IsComplete(complete, justCompleted))
            {
                _active.Add(justCompleted);
            }
        }

        /// <summary>
        /// Makes a complete set active; with every slot taken, it replaces the one active the longest.
        /// </summary>
        /// <returns>False if the set isn't complete or there are no slots at all.</returns>
        public bool Activate(int set, IReadOnlyList<bool> complete)
        {
            if (!IsComplete(complete, set) || Slots <= 0)
            {
                return false;
            }

            if (_active.Contains(set))
            {
                return true;
            }

            while (_active.Count >= Slots)
            {
                _active.RemoveAt(0);
            }

            _active.Add(set);
            return true;
        }

        /// <summary>Puts back the active sets from a save, keeping only complete ones and no more than the slots.</summary>
        public void Restore(IReadOnlyList<int> saved, IReadOnlyList<bool> complete)
        {
            _active.Clear();
            foreach (int set in saved)
            {
                if (_active.Count < Slots && IsComplete(complete, set) && !_active.Contains(set))
                {
                    _active.Add(set);
                }
            }
        }

        private static bool IsComplete(IReadOnlyList<bool> complete, int set)
        {
            return set >= 0 && set < complete.Count && complete[set];
        }
    }
}
