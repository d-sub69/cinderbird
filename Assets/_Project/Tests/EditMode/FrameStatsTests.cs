using Cinderbird.Core;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class FrameStatsTests
    {
        [Test]
        public void Empty_ReportsZeros()
        {
            FrameStats stats = new FrameStats(10);

            Assert.That(stats.Count, Is.Zero);
            Assert.That(stats.Average, Is.Zero);
            Assert.That(stats.Max, Is.Zero);
            Assert.That(stats.Percentile(0.95f), Is.Zero);
        }

        [Test]
        public void AverageAndMax_OverTheSamples()
        {
            FrameStats stats = new FrameStats(10);
            stats.Add(10f);
            stats.Add(20f);
            stats.Add(30f);

            Assert.That(stats.Average, Is.EqualTo(20f).Within(1e-5f));
            Assert.That(stats.Max, Is.EqualTo(30f));
        }

        [Test]
        public void Percentile95_OfAHundredFrames_IsTheNinetyFifthSmallest()
        {
            FrameStats stats = new FrameStats(100);
            // Added out of order to make sure it sorts.
            for (int i = 100; i >= 1; i--)
            {
                stats.Add(i);
            }

            Assert.That(stats.Percentile(0.95f), Is.EqualTo(95f));
            Assert.That(stats.Percentile(1f), Is.EqualTo(100f));
            Assert.That(stats.Percentile(0f), Is.EqualTo(1f));
        }

        [Test]
        public void Percentile_ShowsAStutterTheAverageHides()
        {
            FrameStats stats = new FrameStats(100);
            for (int i = 0; i < 90; i++)
            {
                stats.Add(5f);
            }

            for (int i = 0; i < 10; i++)
            {
                stats.Add(40f);
            }

            Assert.That(stats.Average, Is.LessThan(10f));
            Assert.That(stats.Percentile(0.95f), Is.EqualTo(40f));
        }

        [Test]
        public void WhenFull_TheOldestFramesDropOut()
        {
            FrameStats stats = new FrameStats(3);
            stats.Add(100f);
            stats.Add(1f);
            stats.Add(1f);
            stats.Add(1f);

            Assert.That(stats.Count, Is.EqualTo(3));
            Assert.That(stats.Max, Is.EqualTo(1f));
        }

        [Test]
        public void Clear_StartsOver()
        {
            FrameStats stats = new FrameStats(3);
            stats.Add(50f);

            stats.Clear();
            stats.Add(2f);

            Assert.That(stats.Count, Is.EqualTo(1));
            Assert.That(stats.Max, Is.EqualTo(2f));
        }
    }
}
