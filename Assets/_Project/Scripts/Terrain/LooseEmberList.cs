using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// The embers lying loose on the mountain, each with an id that stays the same while it lies there. Finding one
    /// by id is constant time, so many loose embers (e.g. Sparks slipping through the mesh) stay cheap. Removing one
    /// moves the last into its place, so the order may change.
    /// </summary>
    public sealed class LooseEmberList
    {
        private readonly List<LooseEmber> _items = new List<LooseEmber>();
        private readonly Dictionary<int, int> _indexById = new Dictionary<int, int>();
        private int _nextId;

        public IReadOnlyList<LooseEmber> Items => _items;

        public int Count => _items.Count;

        public LooseEmber this[int index] => _items[index];

        /// <returns>The new ember's id.</returns>
        public int Add(Ember ember, Vector3 cameFrom)
        {
            int id = _nextId++;
            _indexById[id] = _items.Count;
            _items.Add(new LooseEmber(id, ember, cameFrom));
            return id;
        }

        /// <returns>The index of the ember with this id, or -1 if it isn't loose (any more).</returns>
        public int IndexOf(int id)
        {
            return _indexById.TryGetValue(id, out int index) ? index : -1;
        }

        public void MoveTo(int index, Vector3 position)
        {
            _items[index] = _items[index].MovedTo(position);
        }

        public void SetRest(int index, float rest)
        {
            _items[index] = _items[index].WithRest(rest);
        }

        public void RemoveAt(int index)
        {
            _indexById.Remove(_items[index].Id);
            int last = _items.Count - 1;
            if (index != last)
            {
                _items[index] = _items[last];
                _indexById[_items[index].Id] = index;
            }

            _items.RemoveAt(last);
        }

        public void Clear()
        {
            _items.Clear();
            _indexById.Clear();
        }
    }
}
