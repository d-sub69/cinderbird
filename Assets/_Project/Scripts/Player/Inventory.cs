using System;
using System.Collections.Generic;

namespace Cinderbird.Player
{
    /// <summary>
    /// Fixed number of slots, each holding at most one item, with one active slot (what's in hand).
    /// The active slot may be empty, meaning empty hands. Generic so it can be tested with plain values.
    /// </summary>
    public sealed class Inventory<T> where T : class
    {
        private readonly T[] _slots;

        public Inventory(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "An inventory needs at least one slot.");
            }

            _slots = new T[capacity];
        }

        public event Action Changed;

        public int Capacity => _slots.Length;

        public int ActiveSlot { get; private set; }

        /// <summary>The item in hand, or null for empty hands.</summary>
        public T Active => _slots[ActiveSlot];

        public T this[int slot] => _slots[slot];

        public bool HasFreeSlot => Array.IndexOf(_slots, null) >= 0;

        /// <summary>Puts the item into the first empty slot.</summary>
        /// <returns>False if it's already inside or every slot is taken.</returns>
        public bool TryAdd(T item, out int slot)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            slot = IndexOf(item);
            if (slot >= 0)
            {
                return false;
            }

            slot = Array.IndexOf(_slots, null);
            if (slot < 0)
            {
                return false;
            }

            _slots[slot] = item;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Empties the item's slot; if it was in hand, the hands become empty.</summary>
        public bool Remove(T item)
        {
            int slot = IndexOf(item);
            if (slot < 0)
            {
                return false;
            }

            _slots[slot] = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Replaces every slot with saved contents (null = empty) and selects the saved active slot. Extra entries
        /// beyond the capacity and repeats of an item are dropped; fires <see cref="Changed"/> once.
        /// </summary>
        public void Restore(IReadOnlyList<T> slots, int activeSlot)
        {
            Array.Clear(_slots, 0, _slots.Length);
            for (int i = 0; i < slots.Count && i < _slots.Length; i++)
            {
                T item = slots[i];
                if (item != null && IndexOf(item) < 0)
                {
                    _slots[i] = item;
                }
            }

            ActiveSlot = activeSlot >= 0 && activeSlot < _slots.Length ? activeSlot : 0;
            Changed?.Invoke();
        }

        public int IndexOf(T item)
        {
            return item == null ? -1 : Array.IndexOf(_slots, item);
        }

        /// <summary>Makes a slot active, even an empty one (empty hands). Out-of-range slots are ignored.</summary>
        public void Select(int slot)
        {
            if (slot < 0 || slot >= _slots.Length || slot == ActiveSlot)
            {
                return;
            }

            ActiveSlot = slot;
            Changed?.Invoke();
        }

        /// <summary>Moves to the next filled slot in a direction (+1 or -1), wrapping around; skips empty slots.</summary>
        public void Cycle(int direction)
        {
            if (direction == 0)
            {
                return;
            }

            int step = direction > 0 ? 1 : -1;
            for (int offset = 1; offset < _slots.Length; offset++)
            {
                int slot = ((ActiveSlot + step * offset) % _slots.Length + _slots.Length) % _slots.Length;
                if (_slots[slot] != null)
                {
                    Select(slot);
                    return;
                }
            }
        }
    }
}
