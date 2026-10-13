using System;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Which ash layers can be dug. Only the outermost starts open; deeper layers open in order (by Heat level
    /// in the full game, by a debug key for now). Ash and embers in a locked layer can't be taken.
    /// </summary>
    public sealed class LayerLocks
    {
        private readonly bool[] _unlocked;

        public LayerLocks(int layerCount)
        {
            if (layerCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(layerCount), "There must be at least one layer.");
            }

            _unlocked = new bool[layerCount];
            _unlocked[0] = true;
        }

        public int Count => _unlocked.Length;

        /// <summary>The deepest layer that is open; layers open strictly in order.</summary>
        public int DeepestUnlocked
        {
            get
            {
                int deepest = 0;
                while (deepest + 1 < _unlocked.Length && _unlocked[deepest + 1])
                {
                    deepest++;
                }

                return deepest;
            }
        }

        /// <summary>Layers outside the known range count as open, so unexpected data never blocks digging.</summary>
        public bool IsUnlocked(int layer)
        {
            return layer < 0 || layer >= _unlocked.Length || _unlocked[layer];
        }

        /// <returns>The layer that was opened, or -1 if every layer is already open.</returns>
        public int UnlockNext()
        {
            int next = DeepestUnlocked + 1;
            if (next >= _unlocked.Length)
            {
                return -1;
            }

            _unlocked[next] = true;
            return next;
        }
    }
}
