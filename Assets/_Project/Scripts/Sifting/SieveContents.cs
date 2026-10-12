using System;
using System.Collections.Generic;
using Cinderbird.Embers;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// What is in the sieve: loose ash and the embers caught with it.
    /// Rule: you can only scoop into an ash-free sieve, so the loop is scoop → shake → scoop.
    /// Embers pile up across scoops until tipped out.
    /// </summary>
    public sealed class SieveContents
    {
        private readonly List<Ember> _embers = new List<Ember>();

        public event Action Changed;

        /// <summary>Cubic meters of ash still in the sieve.</summary>
        public float AshVolume { get; private set; }

        public IReadOnlyList<Ember> Embers => _embers;

        public bool CanScoop => AshVolume <= 0f;

        /// <returns>False (and nothing changes) when the sieve still holds ash.</returns>
        public bool TryAdd(float ashVolume, List<Ember> embers)
        {
            if (ashVolume < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(ashVolume), "Ash volume cannot be negative.");
            }

            if (!CanScoop)
            {
                return false;
            }

            AshVolume += ashVolume;
            _embers.AddRange(embers);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Moves up to <paramref name="count"/> of the most recently added embers into <paramref name="output"/>
        /// (appended, oldest of them first). Taking from the end keeps the remaining embers at the same
        /// indices, which the views rely on to keep each ember in its spot.
        /// </summary>
        /// <returns>How many embers were taken.</returns>
        public int TakeNewestEmbers(int count, List<Ember> output)
        {
            int taken = Math.Min(Math.Max(0, count), _embers.Count);
            if (taken == 0)
            {
                return 0;
            }

            int start = _embers.Count - taken;
            for (int i = start; i < _embers.Count; i++)
            {
                output.Add(_embers[i]);
            }

            _embers.RemoveRange(start, taken);
            Changed?.Invoke();
            return taken;
        }

        /// <returns>The ash actually removed, which is less than asked if the sieve runs out.</returns>
        public float RemoveAsh(float volume)
        {
            if (volume <= 0f || AshVolume <= 0f)
            {
                return 0f;
            }

            float removed = Math.Min(volume, AshVolume);
            AshVolume -= removed;
            Changed?.Invoke();
            return removed;
        }
    }
}
