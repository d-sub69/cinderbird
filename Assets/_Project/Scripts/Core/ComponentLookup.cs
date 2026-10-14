using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Core
{
    /// <summary>
    /// Remembers which component (looked up in a collider's parents) belongs to each collider, for code that asks
    /// every frame about what a ray or overlap hit. GetComponentInParent walks the hierarchy each call, and in the
    /// Editor allocates when it finds nothing, which would show up as per-frame garbage.
    /// </summary>
    /// <remarks>
    /// Assumes the components don't come and go at runtime on the colliders asked about (true for the nest, tools
    /// and terrain chunks). Bounded, so colliders created and destroyed over a long session can't pile up.
    /// </remarks>
    public sealed class ComponentLookup<T> where T : class
    {
        private const int MaxEntries = 512;

        private readonly Dictionary<Collider, T> _byCollider = new Dictionary<Collider, T>();

        /// <summary>The <typeparamref name="T"/> on the collider or one of its parents, or null.</summary>
        public T Find(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            if (_byCollider.TryGetValue(collider, out T found))
            {
                return found;
            }

            if (_byCollider.Count >= MaxEntries)
            {
                _byCollider.Clear();
            }

            found = collider.GetComponentInParent<T>();
            // Unity's "destroyed or missing" stand-in compares equal to null but isn't; store a real null.
            if (found is Object unityObject && unityObject == null)
            {
                found = null;
            }

            _byCollider[collider] = found;
            return found;
        }
    }
}
