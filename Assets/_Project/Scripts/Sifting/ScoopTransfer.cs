using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// A scooped load on its way from the pile into the sieve. The ash and embers are held here while the pour
    /// plays, then handed to the sieve in one go, so the embers arrive already buried under a full layer of ash.
    /// </summary>
    public sealed class ScoopTransfer
    {
        private readonly List<Ember> _embers = new List<Ember>();
        private float _duration;
        private float _elapsed;

        public bool IsActive { get; private set; }

        /// <summary>Ash volume of the load in flight, in cubic meters.</summary>
        public float Volume { get; private set; }

        public IReadOnlyList<Ember> Embers => _embers;

        /// <summary>Color of the load's ash, from the layers it was dug from.</summary>
        public Color AshColor { get; private set; }

        public Color DustColor { get; private set; }

        /// <summary>0 when the pour starts, 1 when the load has fully arrived; 0 when idle.</summary>
        public float Progress => !IsActive ? 0f : _duration <= 0f ? 1f : Math.Min(1f, _elapsed / _duration);

        /// <returns>False if a load is already in flight.</returns>
        public bool TryBegin(float volume, List<Ember> embers, float duration)
        {
            return TryBegin(volume, embers, duration, AshColors.DefaultAsh, AshColors.DefaultDust);
        }

        /// <returns>False if a load is already in flight.</returns>
        public bool TryBegin(float volume, List<Ember> embers, float duration, Color ashColor, Color dustColor)
        {
            if (volume < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(volume), "Ash volume cannot be negative.");
            }

            if (IsActive)
            {
                return false;
            }

            // Copied: the caller reuses its list for the next dig.
            _embers.Clear();
            _embers.AddRange(embers);
            Volume = volume;
            AshColor = ashColor;
            DustColor = dustColor;
            _duration = Math.Max(0f, duration);
            _elapsed = 0f;
            IsActive = true;
            return true;
        }

        /// <returns>True once the load has fully arrived and is ready to hand over.</returns>
        public bool Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return false;
            }

            _elapsed += Math.Max(0f, deltaTime);
            return _elapsed >= _duration;
        }

        /// <summary>Puts the load into the sieve (finishing early if needed) and goes idle.</summary>
        /// <returns>False if nothing was in flight.</returns>
        public bool HandOverTo(SieveContents sieve)
        {
            if (!IsActive)
            {
                return false;
            }

            // Reset first so listeners reacting to the sieve change already see the transfer as finished.
            IsActive = false;
            float volume = Volume;
            Volume = 0f;
            sieve.TryAdd(volume, _embers, AshColor, DustColor);
            _embers.Clear();
            return true;
        }
    }
}
