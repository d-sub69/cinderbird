using System;
using System.Collections.Generic;

namespace Cinderbird.Offerings
{
    /// <summary>
    /// The state of every offering, by catalog index, and the moves between states: uncovered by a dig (lying on the pile),
    /// picked up by the player into the shrine's collection, placed on the shelf and taken off, sold and bought back.
    /// Nothing is ever lost for good: a sold offering can be bought back. Glow is the caller's business.
    /// </summary>
    public sealed class OfferingCollection
    {
        private readonly OfferingState[] _states;

        public OfferingCollection(int count)
        {
            _states = new OfferingState[count];
        }

        /// <summary>Fired with the index of an offering whose state changed (or -1 after <see cref="Restore"/>).</summary>
        public event Action<int> Changed;

        public int Count => _states.Length;

        public OfferingState StateOf(int index)
        {
            return _states[index];
        }

        /// <summary>A dig reached it: it lies on the pile until the player picks it up.</summary>
        public bool Uncover(int index) => Move(index, OfferingState.Buried, OfferingState.OnPile);

        /// <summary>The player picked it up from the pile and took it to the shrine.</summary>
        public bool PickUp(int index) => Move(index, OfferingState.OnPile, OfferingState.InCollection);

        public bool Place(int index) => Move(index, OfferingState.InCollection, OfferingState.OnShelf);

        public bool TakeOff(int index) => Move(index, OfferingState.OnShelf, OfferingState.InCollection);

        public bool Sell(int index) => Move(index, OfferingState.InCollection, OfferingState.Sold);

        public bool BuyBack(int index) => Move(index, OfferingState.Sold, OfferingState.InCollection);

        /// <summary>Whether every listed offering is on the shelf (a set is complete); an empty list never is.</summary>
        public bool AllOnShelf(IReadOnlyList<int> indices)
        {
            return indices.Count > 0 && CountOnShelf(indices) == indices.Count;
        }

        public int CountOnShelf(IReadOnlyList<int> indices)
        {
            int count = 0;
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] >= 0 && indices[i] < _states.Length && _states[indices[i]] == OfferingState.OnShelf)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Sets every state at once (from a save); missing entries stay buried.</summary>
        public void Restore(IReadOnlyList<OfferingState> states)
        {
            for (int i = 0; i < _states.Length; i++)
            {
                _states[i] = i < states.Count ? states[i] : OfferingState.Buried;
            }

            Changed?.Invoke(-1);
        }

        private bool Move(int index, OfferingState from, OfferingState to)
        {
            if (index < 0 || index >= _states.Length || _states[index] != from)
            {
                return false;
            }

            _states[index] = to;
            Changed?.Invoke(index);
            return true;
        }
    }
}
