using System;

namespace Cinderbird.Economy
{
    /// <summary>
    /// The player's two counters: Glow (spendable currency) and Heat (overall progress toward the rebirth).
    /// </summary>
    public sealed class Currencies
    {
        public event Action Changed;

        public int Glow { get; private set; }

        public int Heat { get; private set; }

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
    }
}
