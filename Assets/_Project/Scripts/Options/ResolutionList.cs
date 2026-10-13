using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Options
{
    /// <summary>
    /// Turns the display's mode list (which repeats each size once per refresh rate) into the sizes shown in the
    /// options menu: each width × height once, smallest first.
    /// </summary>
    public static class ResolutionList
    {
        public static List<Vector2Int> Unique(IEnumerable<Vector2Int> sizes)
        {
            List<Vector2Int> unique = new List<Vector2Int>();
            foreach (Vector2Int size in sizes)
            {
                if (size.x > 0 && size.y > 0 && !unique.Contains(size))
                {
                    unique.Add(size);
                }
            }

            unique.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return unique;
        }

        /// <returns>The index of the listed size closest in pixel count to the wanted one; -1 for an empty list.</returns>
        public static int IndexOfClosest(IReadOnlyList<Vector2Int> sizes, int width, int height)
        {
            int best = -1;
            long bestDifference = long.MaxValue;
            long wanted = (long)width * height;
            for (int i = 0; i < sizes.Count; i++)
            {
                if (sizes[i].x == width && sizes[i].y == height)
                {
                    return i;
                }

                long difference = System.Math.Abs((long)sizes[i].x * sizes[i].y - wanted);
                if (difference < bestDifference)
                {
                    best = i;
                    bestDifference = difference;
                }
            }

            return best;
        }
    }
}
