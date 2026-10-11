using System.Collections.Generic;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Hands out free spot indices in random order, so each new ember lands somewhere unpredictable
    /// but never on an occupied spot.
    /// </summary>
    public sealed class SieveSpotAssigner
    {
        private readonly List<int> _free;
        private readonly System.Random _random;

        public SieveSpotAssigner(int spotCount, System.Random random)
        {
            _random = random;
            _free = new List<int>(spotCount);
            for (int i = 0; i < spotCount; i++)
            {
                _free.Add(i);
            }
        }

        public int FreeCount => _free.Count;

        /// <returns>A random free spot index, or -1 when every spot is taken.</returns>
        public int TakeRandom()
        {
            if (_free.Count == 0)
            {
                return -1;
            }

            int pick = _random.Next(_free.Count);
            int spot = _free[pick];
            int last = _free.Count - 1;
            _free[pick] = _free[last];
            _free.RemoveAt(last);
            return spot;
        }

        public void Release(int spot)
        {
            _free.Add(spot);
        }
    }
}
