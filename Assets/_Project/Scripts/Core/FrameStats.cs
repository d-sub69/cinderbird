using System;

namespace Cinderbird.Core
{
    /// <summary>
    /// The last N frame times in a ring buffer, with the average, 95th percentile and worst. The 95th percentile
    /// shows stutter that an average hides: one slow frame in twenty is felt even at a good average.
    /// </summary>
    public sealed class FrameStats
    {
        private readonly float[] _samples;
        private readonly float[] _sorted;
        private int _next;

        public FrameStats(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Need room for at least one frame.");
            }

            _samples = new float[capacity];
            _sorted = new float[capacity];
        }

        public int Count { get; private set; }

        public void Add(float milliseconds)
        {
            _samples[_next] = milliseconds;
            _next = (_next + 1) % _samples.Length;
            Count = Math.Min(Count + 1, _samples.Length);
        }

        public void Clear()
        {
            Count = 0;
            _next = 0;
        }

        public float Average
        {
            get
            {
                if (Count == 0)
                {
                    return 0f;
                }

                double sum = 0d;
                for (int i = 0; i < Count; i++)
                {
                    sum += _samples[i];
                }

                return (float)(sum / Count);
            }
        }

        public float Max
        {
            get
            {
                float max = 0f;
                for (int i = 0; i < Count; i++)
                {
                    max = Math.Max(max, _samples[i]);
                }

                return max;
            }
        }

        /// <summary>The time that this share of frames (0..1) stays at or under, e.g. 0.95 for the 95th percentile.</summary>
        /// <remarks>Sorts a copy, so call it a few times per second, not per frame. Doesn't allocate.</remarks>
        public float Percentile(float share)
        {
            if (Count == 0)
            {
                return 0f;
            }

            Array.Copy(_samples, _sorted, Count);
            Array.Sort(_sorted, 0, Count);
            // Nearest-rank method: the smallest value with at least `share` of the samples at or below it.
            int rank = (int)Math.Ceiling(Math.Min(Math.Max(share, 0f), 1f) * Count);
            return _sorted[Math.Max(0, rank - 1)];
        }
    }
}
