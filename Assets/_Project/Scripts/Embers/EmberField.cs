using System.Collections;
using System.Collections.Generic;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// The embers still buried in one chunk. Order is not preserved: removal swaps with the last element.
    /// </summary>
    public sealed class EmberField : IReadOnlyList<Ember>
    {
        private readonly List<Ember> _embers = new List<Ember>();

        public int Count => _embers.Count;

        public Ember this[int index] => _embers[index];

        // Interface enumeration boxes the enumerator; hot paths should index with Count instead.
        public IEnumerator<Ember> GetEnumerator()
        {
            return _embers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(Ember ember)
        {
            _embers.Add(ember);
        }

        public void AddRange(List<Ember> embers)
        {
            _embers.AddRange(embers);
        }

        /// <summary>Removes one ember equal to <paramref name="ember"/> (same position, tier and layer).</summary>
        /// <returns>False if no such ember is here (e.g. it was dug out meanwhile).</returns>
        public bool Remove(Ember ember)
        {
            for (int i = _embers.Count - 1; i >= 0; i--)
            {
                Ember candidate = _embers[i];
                if (candidate.Position.Equals(ember.Position) && candidate.TierIndex == ember.TierIndex && candidate.Layer == ember.Layer)
                {
                    int last = _embers.Count - 1;
                    _embers[i] = _embers[last];
                    _embers.RemoveAt(last);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Swaps the contents for another set, e.g. the embers from a save.</summary>
        public void ReplaceWith(IReadOnlyList<Ember> embers)
        {
            _embers.Clear();
            for (int i = 0; i < embers.Count; i++)
            {
                _embers.Add(embers[i]);
            }
        }

        /// <summary>Moves every ember of a layer above <paramref name="layer"/> (index lower) into <paramref name="output"/>.</summary>
        /// <returns>How many embers were extracted.</returns>
        public int ExtractLayersAbove(int layer, List<Ember> output)
        {
            int extracted = 0;
            for (int i = _embers.Count - 1; i >= 0; i--)
            {
                if (_embers[i].Layer >= layer)
                {
                    continue;
                }

                output.Add(_embers[i]);
                int last = _embers.Count - 1;
                _embers[i] = _embers[last];
                _embers.RemoveAt(last);
                extracted++;
            }

            return extracted;
        }

        /// <summary>
        /// Moves every ember within the sphere into <paramref name="output"/> (appended, not cleared).
        /// </summary>
        /// <returns>How many embers were extracted.</returns>
        public int ExtractWithin(Vector3 center, float radius, List<Ember> output)
        {
            return ExtractWithin(center, radius, output, null);
        }

        /// <param name="locks">Embers in a locked layer stay buried; null takes everything.</param>
        public int ExtractWithin(Vector3 center, float radius, List<Ember> output, LayerLocks locks)
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

                if (locks != null && !locks.IsUnlocked(ember.Layer))
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
