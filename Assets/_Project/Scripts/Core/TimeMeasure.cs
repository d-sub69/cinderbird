using System.Diagnostics;

namespace Cinderbird.Core
{
    /// <summary>Wall-clock timing for the performance read-outs; safe on any thread and allocation-free.</summary>
    public static class TimeMeasure
    {
        /// <summary>A start point to pass to <see cref="MillisecondsSince"/>.</summary>
        public static long Now()
        {
            return Stopwatch.GetTimestamp();
        }

        public static float MillisecondsSince(long startTimestamp)
        {
            return (float)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency);
        }
    }
}
