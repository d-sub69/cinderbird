using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// A coarse spatial hash of points (cluster centers by index), so "anything near here?" looks at a few neighbors
    /// instead of every cluster. Cells are at least as wide as the largest distance asked about.
    /// </summary>
    public sealed class ClusterGrid
    {
        private readonly float _cellSize;
        private readonly Dictionary<Vector3Int, List<int>> _cells = new Dictionary<Vector3Int, List<int>>();
        private readonly List<int> _near = new List<int>();

        public ClusterGrid(float cellSize)
        {
            _cellSize = Mathf.Max(0.1f, cellSize);
        }

        public void Add(Vector3 point, int index)
        {
            Vector3Int cell = CellOf(point);
            if (!_cells.TryGetValue(cell, out List<int> list))
            {
                list = new List<int>();
                _cells[cell] = list;
            }

            list.Add(index);
        }

        /// <summary>Indices in the point's cell and the 26 around it (valid until the next call).</summary>
        public List<int> Near(Vector3 point)
        {
            _near.Clear();
            Vector3Int cell = CellOf(point);
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        if (_cells.TryGetValue(cell + new Vector3Int(x, y, z), out List<int> list))
                        {
                            _near.AddRange(list);
                        }
                    }
                }
            }

            return _near;
        }

        private Vector3Int CellOf(Vector3 point)
        {
            return new Vector3Int(Mathf.FloorToInt(point.x / _cellSize), Mathf.FloorToInt(point.y / _cellSize),
                Mathf.FloorToInt(point.z / _cellSize));
        }
    }
}
