using System;
using System.Collections.Generic;
using Cinderbird.Embers;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Embers carried in the basket on the way to the nest. Holds a limited number, so full baskets
    /// force a trip. Both a pour source (into the nest) and a pour target (from the sieve).
    /// </summary>
    public sealed class BasketContents : IEmberSource, IEmberSink
    {
        private readonly List<Ember> _embers;

        public BasketContents(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Basket must hold at least one ember.");
            }

            Capacity = capacity;
            _embers = new List<Ember>(capacity);
        }

        public event Action Changed;

        public int Capacity { get; }

        public IReadOnlyList<Ember> Embers => _embers;

        public int FreeSpace => Capacity - _embers.Count;

        public bool IsFull => FreeSpace == 0;

        public int EmberCount => _embers.Count;

        /// <summary>Only shaken-out embers ever go in, so a basket never holds ash.</summary>
        public bool HasAsh => false;

        /// <summary>
        /// Moves up to <paramref name="count"/> of the most recently added embers into <paramref name="output"/>
        /// (appended, oldest of them first). Taking from the end keeps the rest at their indices, and spots.
        /// </summary>
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

        /// <summary>Empties the basket into <paramref name="output"/> (appended).</summary>
        /// <returns>How many embers were taken.</returns>
        public int TakeAll(List<Ember> output)
        {
            int count = _embers.Count;
            if (count == 0)
            {
                return 0;
            }

            output.AddRange(_embers);
            _embers.Clear();
            Changed?.Invoke();
            return count;
        }

        /// <summary>Replaces the contents with a saved state, keeping only as many embers as fit.</summary>
        /// <returns>How many saved embers didn't fit (e.g. the capacity was lowered since the save).</returns>
        public int Restore(IReadOnlyList<Ember> embers)
        {
            _embers.Clear();
            int kept = Math.Min(embers.Count, Capacity);
            for (int i = 0; i < kept; i++)
            {
                _embers.Add(embers[i]);
            }

            Changed?.Invoke();
            return embers.Count - kept;
        }

        /// <exception cref="InvalidOperationException">When the embers don't fit; check <see cref="FreeSpace"/> first.</exception>
        public void Add(List<Ember> embers)
        {
            if (embers.Count > FreeSpace)
            {
                throw new InvalidOperationException($"Basket has room for {FreeSpace} embers, not {embers.Count}.");
            }

            if (embers.Count == 0)
            {
                return;
            }

            _embers.AddRange(embers);
            Changed?.Invoke();
        }
    }
}
