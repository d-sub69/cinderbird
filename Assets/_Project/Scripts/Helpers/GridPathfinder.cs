using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// A* over an <see cref="INavGrid"/>, eight directions without cutting corners, with a cost for climbing. All search
    /// state lives in arrays reused between searches (a search number marks what is current), so a search allocates
    /// nothing once the arrays exist. <see cref="Smooth"/> then drops the waypoints a walker can skip in a straight line.
    /// </summary>
    public sealed class GridPathfinder
    {
        private const float Diagonal = 1.41421356f;
        private static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] StepZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private readonly int _maxExpanded;
        private readonly float _climbCost;
        private float[] _cost = new float[0];
        private int[] _parent = new int[0];
        private int[] _seen = new int[0];
        private bool[] _closed = new bool[0];
        private int[] _heap = new int[0];
        private float[] _heapKey = new float[0];
        private int _heapCount;
        private int _search;

        /// <param name="maxExpanded">Cells a search may expand before it gives up; bounds the worst case.</param>
        /// <param name="climbCost">Extra cost per meter climbed or descended, so flat ground is preferred.</param>
        public GridPathfinder(int maxExpanded, float climbCost)
        {
            _maxExpanded = Mathf.Max(1, maxExpanded);
            _climbCost = Mathf.Max(0f, climbCost);
        }

        /// <summary>Cells the last search expanded, for tuning.</summary>
        public int LastExpanded { get; private set; }

        /// <param name="path">Cleared and filled with the cells from the start to the goal, both included.</param>
        /// <returns>False if no goal cell can be reached (within the expansion limit).</returns>
        public bool FindPath(INavGrid grid, Vector2Int start, IPathGoal goal, List<Vector2Int> path)
        {
            path.Clear();
            int width = grid.Width;
            int count = width * grid.Depth;
            if (start.x < 0 || start.y < 0 || start.x >= width || start.y >= grid.Depth)
            {
                return false;
            }

            EnsureCapacity(count);
            _search++;
            _heapCount = 0;
            LastExpanded = 0;

            int startIndex = start.x + start.y * width;
            Open(startIndex, 0f, -1, goal.Estimate(start.x, start.y));
            while (_heapCount > 0)
            {
                int current = Pop();
                if (_closed[current])
                {
                    continue;
                }

                _closed[current] = true;
                int x = current % width;
                int z = current / width;
                if (goal.IsGoal(x, z))
                {
                    Trace(current, width, path);
                    return true;
                }

                if (++LastExpanded > _maxExpanded)
                {
                    return false;
                }

                for (int d = 0; d < 8; d++)
                {
                    int nx = x + StepX[d];
                    int nz = z + StepZ[d];
                    if (nx < 0 || nz < 0 || nx >= width || nz >= grid.Depth)
                    {
                        continue;
                    }

                    int next = nx + nz * width;
                    if (_seen[next] == _search && _closed[next])
                    {
                        continue;
                    }

                    bool diagonal = d >= 4;
                    // No cutting corners: a diagonal step needs both cells beside it to be passable too.
                    if (!grid.CanStep(x, z, nx, nz) ||
                        (diagonal && (!grid.CanStep(x, z, nx, z) || !grid.CanStep(x, z, x, nz))))
                    {
                        continue;
                    }

                    float climb = Mathf.Abs(grid.HeightAt(nx, nz) - grid.HeightAt(x, z));
                    float cost = _cost[current] + (diagonal ? Diagonal : 1f) + climb * _climbCost;
                    if (_seen[next] == _search && cost >= _cost[next])
                    {
                        continue;
                    }

                    Open(next, cost, current, cost + goal.Estimate(nx, nz));
                }
            }

            return false;
        }

        /// <summary>Keeps only the cells where the path has to turn: the walker goes straight between them.</summary>
        /// <param name="output">Cleared and filled; the first and last cells are always kept.</param>
        public static void Smooth(INavGrid grid, List<Vector2Int> path, List<Vector2Int> output)
        {
            output.Clear();
            if (path.Count == 0)
            {
                return;
            }

            int anchor = 0;
            output.Add(path[0]);
            while (anchor < path.Count - 1)
            {
                int next = anchor + 1;
                // The farthest cell still in a straight, clear line from the anchor.
                for (int candidate = path.Count - 1; candidate > anchor + 1; candidate--)
                {
                    if (HasClearLine(grid, path[anchor], path[candidate]))
                    {
                        next = candidate;
                        break;
                    }
                }

                output.Add(path[next]);
                anchor = next;
            }
        }

        /// <summary>Whether a walker can go in a straight line between two cells, step by step.</summary>
        public static bool HasClearLine(INavGrid grid, Vector2Int from, Vector2Int to)
        {
            int dx = to.x - from.x;
            int dz = to.y - from.y;
            int samples = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) * 4;
            int cx = from.x;
            int cz = from.y;
            for (int s = 1; s <= samples; s++)
            {
                float t = (float)s / samples;
                int x = Mathf.RoundToInt(from.x + dx * t);
                int z = Mathf.RoundToInt(from.y + dz * t);
                if (x == cx && z == cz)
                {
                    continue;
                }

                if (!grid.CanStep(cx, cz, x, z))
                {
                    return false;
                }

                if (x != cx && z != cz && (!grid.CanStep(cx, cz, x, cz) || !grid.CanStep(cx, cz, cx, z)))
                {
                    return false;
                }

                cx = x;
                cz = z;
            }

            return true;
        }

        private void Open(int index, float cost, int parent, float key)
        {
            _seen[index] = _search;
            _closed[index] = false;
            _cost[index] = cost;
            _parent[index] = parent;
            Push(index, key);
        }

        private void Trace(int index, int width, List<Vector2Int> path)
        {
            while (index >= 0)
            {
                path.Add(new Vector2Int(index % width, index / width));
                index = _parent[index];
            }

            path.Reverse();
        }

        private void EnsureCapacity(int count)
        {
            if (_cost.Length >= count)
            {
                return;
            }

            _cost = new float[count];
            _parent = new int[count];
            _seen = new int[count];
            _closed = new bool[count];
            // A cell can sit in the heap more than once (a cheaper way found later); 8 entries per cell is the most.
            _heap = new int[count * 8];
            _heapKey = new float[count * 8];
            _search = 0;
        }

        private void Push(int index, float key)
        {
            if (_heapCount >= _heap.Length)
            {
                return;
            }

            int i = _heapCount++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_heapKey[parent] <= key)
                {
                    break;
                }

                _heap[i] = _heap[parent];
                _heapKey[i] = _heapKey[parent];
                i = parent;
            }

            _heap[i] = index;
            _heapKey[i] = key;
        }

        private int Pop()
        {
            int top = _heap[0];
            _heapCount--;
            if (_heapCount == 0)
            {
                return top;
            }

            int lastIndex = _heap[_heapCount];
            float lastKey = _heapKey[_heapCount];
            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= _heapCount)
                {
                    break;
                }

                if (child + 1 < _heapCount && _heapKey[child + 1] < _heapKey[child])
                {
                    child++;
                }

                if (_heapKey[child] >= lastKey)
                {
                    break;
                }

                _heap[i] = _heap[child];
                _heapKey[i] = _heapKey[child];
                i = child;
            }

            _heap[i] = lastIndex;
            _heapKey[i] = lastKey;
            return top;
        }
    }
}
