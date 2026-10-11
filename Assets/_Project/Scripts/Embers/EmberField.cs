using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// The embers still buried in one chunk. Order is not preserved: removal swaps with the last element.
    /// </summary>
    public sealed class EmberField
    {
        private readonly List<Ember> _embers = new List<Ember>();

        public int Count => _embers.Count;

        public Ember this[int index] => _embers[index];

        public void Add(Ember ember)
        {
            _embers.Add(ember);
        }

        public void AddRange(List<Ember> embers)
        {
            _embers.AddRange(embers);
        }

        /// <summary>
        /// Moves every ember within the sphere into <paramref name="output"/> (appended, not cleared).
        /// </summary>
        /// <returns>How many embers were extracted.</returns>
        public int ExtractWithin(Vector3 center, float radius, List<Ember> output)
        {
            float radiusSquared = radius * radius;
            int extracted = 0;

            // Backwards so swap-removal never skips an element.
            for (int i = _embers.Count - 1; i >= 0; i--)
            {
                Ember ember = _embers[i];
                if ((ember.Position - center).sqrMagnitude > radiusSquared)
                {
                    continue;
                }

                output.Add(ember);
                int last = _embers.Count - 1;
                _embers[i] = _embers[last];
                _embers.RemoveAt(last);
                extracted++;
            }

            return extracted;
        }
    }
}
