using System;

namespace Cinderbird.Pacing
{
    /// <summary>
    /// Collects what happens during play into fixed windows (e.g. a minute each) plus a running total for the session,
    /// on the play clock (the pause doesn't count). The game's pacing log writes one line per closed window; the numbers
    /// are what the pacing simulator is calibrated with.
    /// </summary>
    public sealed class PaceTracker
    {
        /// <summary>The player's scoops less than this many seconds apart count as sifting in a row.</summary>
        public const double SteadyGap = 20.0;

        private readonly double _interval;
        private readonly PaceWindow _window = new PaceWindow();
        private readonly PaceWindow _session = new PaceWindow();
        private double _windowStart;
        private double _lastPlayerScoop = double.NaN;

        /// <param name="interval">Seconds per window.</param>
        /// <param name="now">Play time when tracking starts.</param>
        public PaceTracker(double interval, double now)
        {
            _interval = Math.Max(1.0, interval);
            _windowStart = now;
        }

        /// <summary>Everything since tracking started, up to the last closed window.</summary>
        public PaceWindow Session => _session;

        public void Dig(PaceSource source, float volume, int embers, double now)
        {
            _window.AddDig(source, volume, embers);
            if (source != PaceSource.Player)
            {
                return;
            }

            double gap = now - _lastPlayerScoop;
            if (gap > 0.0 && gap < SteadyGap)
            {
                _window.AddSteadyGap(gap);
            }

            _lastPlayerScoop = now;
        }

        public void Fed(int embers)
        {
            _window.AddFeed(embers);
        }

        /// <summary>Glow went up (earned) or down (spent) by <paramref name="delta"/>.</summary>
        public void GlowChanged(long delta)
        {
            _window.AddGlow(delta);
        }

        public void Purchased()
        {
            _window.AddPurchase();
        }

        /// <summary>Closes the window once it is <c>interval</c> long and starts the next.</summary>
        /// <returns>True with the closed window (a copy) when one closed.</returns>
        public bool TryClose(double now, out PaceWindow closed)
        {
            closed = null;
            if (now - _windowStart < _interval)
            {
                return false;
            }

            _window.Seconds = now - _windowStart;
            _session.AddFrom(_window);
            closed = _window.Copy();
            _window.Clear();
            _windowStart = now;
            return true;
        }
    }
}
