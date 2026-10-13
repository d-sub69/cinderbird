using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// What is in the sieve: loose ash and the embers caught with it.
    /// Rules: you can only scoop into an ash-free sieve (scoop → shake → scoop), and not once it holds its
    /// ember capacity. Embers pile up across scoops until poured out.
    /// </summary>
    public sealed class SieveContents : IEmberSource
    {
        private static readonly Color DefaultAshColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color DefaultDustColor = new Color(0.78f, 0.76f, 0.74f, 0.8f);

        private readonly List<Ember> _embers = new List<Ember>();

        public event Action Changed;

        /// <summary>Cubic meters of ash still in the sieve.</summary>
        public float AshVolume { get; private set; }

        public int EmberCount => _embers.Count;

        public bool HasAsh => AshVolume > 0f;

        public IReadOnlyList<Ember> Embers => _embers;

        /// <summary>Scooping stops once this many embers are in the sieve. Unlimited by default.</summary>
        public int EmberCapacity { get; set; } = int.MaxValue;

        public bool IsFullOfEmbers => _embers.Count >= EmberCapacity;

        public bool CanScoop => AshVolume <= 0f && !IsFullOfEmbers;

        /// <summary>Color of the current load's ash, blended from the layers it was dug from.</summary>
        public Color AshColor { get; private set; } = DefaultAshColor;

        /// <summary>Color of the dust the current load sheds when shaken.</summary>
        public Color DustColor { get; private set; } = DefaultDustColor;

        /// <summary>Ash volume of the current load when it was scooped; each scoop starts a new load.</summary>
        public float LoadVolume { get; private set; }

        /// <summary>Share of the current load still in the sieve: 1 right after scooping, 0 once shaken out.</summary>
        public float AshFraction => LoadVolume > 0f ? AshVolume / LoadVolume : 0f;

        /// <returns>False (and nothing changes) when the sieve still holds ash.</returns>
        public bool TryAdd(float ashVolume, List<Ember> embers)
        {
            return TryAdd(ashVolume, embers, DefaultAshColor, DefaultDustColor);
        }

        /// <summary>
        /// Adds a scooped load. Only the ash rule applies here: the ember limit is checked before digging,
        /// so a load already on its way is never lost.
        /// </summary>
        /// <returns>False (and nothing changes) when the sieve still holds ash.</returns>
        public bool TryAdd(float ashVolume, List<Ember> embers, Color ashColor, Color dustColor)
        {
            if (ashVolume < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(ashVolume), "Ash volume cannot be negative.");
            }

            if (HasAsh)
            {
                return false;
            }

            AshVolume += ashVolume;
            // Scooping is only allowed into an ash-free sieve, so this ash is the whole new load.
            LoadVolume = AshVolume;
            AshColor = ashColor;
            DustColor = dustColor;
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
        public int TakeNewest(int count, List<Ember> output)
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

        /// <summary>Replaces everything with a saved state. The ember capacity isn't saved; it comes from the settings.</summary>
        /// <param name="loadVolume">Size of the load being shaken; raised to <paramref name="ashVolume"/> if smaller.</param>
        public void Restore(float ashVolume, float loadVolume, Color ashColor, Color dustColor, IReadOnlyList<Ember> embers)
        {
            if (ashVolume < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(ashVolume), "Ash volume cannot be negative.");
            }

            AshVolume = ashVolume;
            LoadVolume = Math.Max(loadVolume, ashVolume);
            AshColor = ashColor;
            DustColor = dustColor;
            _embers.Clear();
            for (int i = 0; i < embers.Count; i++)
            {
                _embers.Add(embers[i]);
            }

            Changed?.Invoke();
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
