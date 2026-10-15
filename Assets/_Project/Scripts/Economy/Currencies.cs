using System;

namespace Cinderbird.Economy
{
    /// <summary>
    /// The player's two counters: Glow (spendable currency) and Heat (overall progress toward the rebirth).
    /// </summary>
    public sealed class Currencies
    {
        public event Action Changed;

        /// <summary>Fired after <see cref="Restore"/>, for views that count earnings as they arrive (e.g. the nest fire).</summary>
        public event Action Restored;

        public int Glow { get; private set; }

        public int Heat { get; private set; }

        /// <summary>True while <see cref="Restore"/> raises <see cref="Changed"/>: the change came from a save, not from play.</summary>
        public bool IsRestoring { get; private set; }

        /// <summary>Adds both at once so listeners see one consistent change.</summary>
        public void Add(int glow, int heat)
        {
            if (glow < 0 || heat < 0)
            {
                throw new ArgumentOutOfRangeException(glow < 0 ? nameof(glow) : nameof(heat), "Earned amounts cannot be negative.");
            }

            if (glow == 0 && heat == 0)
            {
                return;
            }

            Glow += glow;
            Heat += heat;
            Changed?.Invoke();
        }

        /// <summary>Spends Glow if there is enough; Heat is never spent.</summary>
        /// <returns>False (and nothing changes) when there isn't enough Glow.</returns>
        public bool TrySpend(int glow)
        {
            if (glow < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(glow), "A price cannot be negative.");
            }

            if (glow > Glow)
            {
                return false;
            }

            if (glow == 0)
            {
                return true;
            }

            Glow -= glow;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sets both counters to saved values.</summary>
        public void Restore(int glow, int heat)
        {
            if (glow < 0 || heat < 0)
            {
                throw new ArgumentOutOfRangeException(glow < 0 ? nameof(glow) : nameof(heat), "Saved amounts cannot be negative.");
            }

            Glow = glow;
            Heat = heat;
            IsRestoring = true;
            try
            {
                Changed?.Invoke();
            }
            finally
            {
                IsRestoring = false;
            }

            Restored?.Invoke();
        }
    }
}
