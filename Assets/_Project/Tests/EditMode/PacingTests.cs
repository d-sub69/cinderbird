using Cinderbird.Pacing;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class PacingTests
    {
        [Test]
        public void Tracker_ClosesAWindowPerInterval_WithItsTotals()
        {
            PaceTracker tracker = new PaceTracker(60.0, 100.0);
            tracker.Dig(PaceSource.Player, 0.16f, 1, 101.0);
            tracker.Dig(PaceSource.Player, 0.16f, 0, 104.0);
            tracker.Dig(PaceSource.Helper, 0.1f, 2, 105.0);
            tracker.Dig(PaceSource.Player, 0.16f, 0, 140.0); // after a long gap: not sifting in a row
            tracker.Fed(3);
            tracker.GlowChanged(5);
            tracker.GlowChanged(-120);
            tracker.Purchased();

            Assert.That(tracker.TryClose(159.0, out _), Is.False, "not a minute yet");
            Assert.That(tracker.TryClose(160.0, out PaceWindow window), Is.True);
            Assert.That(window.Seconds, Is.EqualTo(60.0).Within(1e-9));
            Assert.That(window.Scoops(PaceSource.Player), Is.EqualTo(3));
            Assert.That(window.Volume(PaceSource.Helper), Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(window.TotalEmbers, Is.EqualTo(3));
            Assert.That(window.LitersPerScoop(PaceSource.Player), Is.EqualTo(160f).Within(0.01f));
            Assert.That(window.SecondsPerScoop, Is.EqualTo(3.0).Within(1e-9), "only the 3 s gap counts");
            Assert.That(window.GlowEarned, Is.EqualTo(5));
            Assert.That(window.GlowSpent, Is.EqualTo(120));
            Assert.That(window.Purchases, Is.EqualTo(1));

            Assert.That(tracker.TryClose(220.0, out PaceWindow next), Is.True);
            Assert.That(next.Scoops(PaceSource.Player), Is.Zero, "a fresh window");
            Assert.That(tracker.Session.Scoops(PaceSource.Player), Is.EqualTo(3), "the session keeps adding up");
        }

        [Test]
        public void Text_ClockReadsLikeAClock()
        {
            Assert.That(PaceText.Clock(0), Is.EqualTo("0:00"));
            Assert.That(PaceText.Clock(754.9), Is.EqualTo("12:34"));
            Assert.That(PaceText.Clock(3 * 3600 + 5 * 60 + 7), Is.EqualTo("3:05:07"));
            Assert.That(PaceText.Event(65, "Heat level 2"), Is.EqualTo("[Pace] 1:05 | Heat level 2"));
        }

        // Shakes 100 L a second at full energy with nothing else in the way: the starting 0.16 m³ scoop takes 1.6 s, so
        // 0.1 m³ of ash moves each second, and each cubic meter holds one ember worth 1 Glow and 1 Heat.
        private static PaceBehavior Ideal => new PaceBehavior(0f, 1f, 1f, 0f, 0f, 1f, 1f, 0f, 0f, 1f, 2f);

        private static PaceModel TwoLayers(int secondLayerHeatLevel, PaceUpgrade[] upgrades = null)
        {
            PaceLayer[] layers =
            {
                new PaceLayer("Top", 100f, new[] { 100 }, 0, 0f),
                new PaceLayer("Bottom", 100f, new[] { 100 }, secondLayerHeatLevel, 0.9f),
            };
            return new PaceModel(layers, new[] { 1 }, new[] { 1 }, new[] { false }, new[] { 50, 1000 }, upgrades ?? new PaceUpgrade[0],
                Ideal, 100f, 4f, 0f, 99, 0f);
        }

        [Test]
        public void Simulator_OpensTheNextLayerByTheRule_AndFinishes()
        {
            PaceRun run = PaceSimulator.Run(TwoLayers(1));

            Assert.That(run.LevelReachedAt[0], Is.EqualTo(500.0).Within(2.0), "50 Heat at one ember per 10 s");
            Assert.That(run.LayerOpenedAt[1], Is.EqualTo(900.0).Within(2.0), "90% of the top layer");
            Assert.That(run.Finished, Is.True);
            Assert.That(run.FinishedAt, Is.EqualTo(1900.0).Within(3.0), "then all of the bottom one");
        }

        [Test]
        public void Simulator_BuysAnUpgradeAsSoonAsItCan()
        {
            PaceUpgrade sieve = new PaceUpgrade("sieve", "Sieve", PaceEffect.Sieve, new[] { 30 }, new[] { 0.16f, 0.32f });

            PaceRun run = PaceSimulator.Run(TwoLayers(1, new[] { sieve }));

            Assert.That(run.Purchases, Is.EqualTo(1));
            Assert.That(run.Events.Exists(e => e.Text.StartsWith("bought Sieve") && System.Math.Abs(e.Seconds - 300.0) < 3.0),
                Is.True, "30 Glow after 300 s");
        }

        [Test]
        public void Simulator_ABiggerSieveOnlyHelpsWhatTheShakeDoesntLimit()
        {
            PaceUpgrade sieve = new PaceUpgrade("sieve", "Sieve", PaceEffect.Sieve, new[] { 0 }, new[] { 0.16f, 0.32f });
            PaceLayer[] layers = { new PaceLayer("Only", 100f, new[] { 100 }, 0, 0f) };
            PaceBehavior slowHands = new PaceBehavior(1f, 1f, 1f, 0f, 0f, 1f, 1f, 0f, 0f, 1f, 2f);

            double idealSmall = PaceSimulator.Run(TwoLayers(1)).FinishedAt;
            double idealBig = PaceSimulator.Run(TwoLayers(1, new[] { sieve })).FinishedAt;
            double slowSmall = PaceSimulator.Run(new PaceModel(layers, new[] { 1 }, new[] { 1 }, new[] { false }, new int[0],
                new PaceUpgrade[0], slowHands, 100f, 4f, 0f, 99, 0f)).FinishedAt;
            double slowBig = PaceSimulator.Run(new PaceModel(layers, new[] { 1 }, new[] { 1 }, new[] { false }, new int[0],
                new[] { sieve }, slowHands, 100f, 4f, 0f, 99, 0f)).FinishedAt;

            Assert.That(idealBig, Is.EqualTo(idealSmall).Within(3.0), "shake-bound: twice the scoop takes twice the shaking");
            Assert.That(slowBig, Is.LessThan(slowSmall * 0.85), "the time between scoops is what a bigger sieve saves");
        }

        [Test]
        public void Simulator_SaysWhyItIsStuck()
        {
            PaceRun run = PaceSimulator.Run(TwoLayers(2));

            Assert.That(run.Finished, Is.False);
            Assert.That(run.StopReason, Does.Contain("Bottom needs Heat level 2"));
        }

        // Art's first real-speed playtest: Cold Crust with the hand sieve (176 L scoops, 120 L/s shake), nothing bought,
        // 118 sparks in 20 min 35 s. The calibrated default behavior has to reproduce that.
        [Test]
        public void Simulator_DefaultBehavior_MatchesTheFirstPlaytest()
        {
            PaceLayer[] layers = { new PaceLayer("Cold Crust", 2314f, new[] { 1953 }, 0, 0f) };
            PaceModel model = new PaceModel(layers, new[] { 1 }, new[] { 1 }, new[] { false }, new[] { 118 }, new PaceUpgrade[0],
                PaceBehavior.Default, 120f, 4f, 0.176f, 99, 0f);

            PaceRun run = PaceSimulator.Run(model);

            Assert.That(run.LevelReachedAt[0], Is.EqualTo(1235.0).Within(1235.0 * 0.15), "118 Heat at about 20.5 minutes");
        }

        [Test]
        public void Simulator_TheHelperAndTheWindAddTheirShare()
        {
            PaceLayer[] layers = { new PaceLayer("Only", 100f, new[] { 100 }, 0, 0f) };
            PaceUpgrade hire = new PaceUpgrade("sally", "Sally", PaceEffect.HelperHire, new[] { 0 }, new[] { 0f, 1f });
            PaceModel alone = new PaceModel(layers, new[] { 1 }, new[] { 1 }, new[] { false }, new int[0], new PaceUpgrade[0],
                Ideal, 100f, 1.6f, 0.16f, 99, 0f);
            PaceModel helped = new PaceModel(layers, new[] { 1 }, new[] { 1 }, new[] { false }, new int[0], new[] { hire },
                Ideal, 100f, 1.6f, 0.16f, 99, 0f);

            double aloneTime = PaceSimulator.Run(alone).FinishedAt;
            double helpedTime = PaceSimulator.Run(helped).FinishedAt;

            Assert.That(aloneTime, Is.EqualTo(1000.0).Within(3.0));
            Assert.That(helpedTime, Is.EqualTo(500.0).Within(5.0), "she sifts as fast as the player here");
        }
    }
}
