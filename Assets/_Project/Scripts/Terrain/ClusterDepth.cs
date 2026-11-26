using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// How deep a point lies under the open surface: the shortest way out to air along 14 directions (the axes and the
    /// diagonals). Enough to tell how much ash covers a cluster, and where on the surface it is closest.
    /// </summary>
    public static class ClusterDepth
    {
        private static readonly Vector3[] Directions = CreateDirections();

        /// <param name="reach">Looks no farther; returns more than this when the point is buried deeper.</param>
        /// <param name="surface">The nearest open spot found (the point itself when it's already in air).</param>
        public static float Probe(VoxelWorld world, Vector3 point, float reach, out Vector3 surface)
        {
            surface = point;
            if (world.SampleDensity(point) <= 0f)
            {
                return 0f;
            }

            float step = world.CellSize * 0.5f;
            // The mountain stands on rock: air under the floor is no way out.
            float floor = world.Origin.y + world.CellSize;
            float best = float.MaxValue;
            foreach (Vector3 direction in Directions)
            {
                for (float distance = step; distance <= reach && distance < best; distance += step)
                {
                    Vector3 probe = point + direction * distance;
                    if (probe.y < floor || world.SampleDensity(probe) > 0f)
                    {
                        continue;
                    }

                    best = distance;
                    surface = probe;
                    break;
                }
            }

            return best < float.MaxValue ? best : reach + step;
        }

        private static Vector3[] CreateDirections()
        {
            Vector3[] directions = new Vector3[14];
            int i = 0;
            directions[i++] = Vector3.up;
            directions[i++] = Vector3.down;
            directions[i++] = Vector3.left;
            directions[i++] = Vector3.right;
            directions[i++] = Vector3.forward;
            directions[i++] = Vector3.back;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        directions[i++] = new Vector3(x, y, z).normalized;
                    }
                }
            }

            return directions;
        }
    }
}
