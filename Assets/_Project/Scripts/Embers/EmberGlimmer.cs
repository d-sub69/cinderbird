using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Finds where a buried ember is close enough to the surface to show through.
    /// Density saturates one cell into the ash, so depth can't be read from density directly;
    /// instead we probe outward in a fixed set of directions and look for air.
    /// </summary>
    public static class EmberGlimmer
    {
        private const int SearchSteps = 10;

        // 6 axes + 8 diagonals: enough coverage for a fist-sized search radius without missing obvious holes.
        private static readonly Vector3[] ProbeDirections = CreateProbeDirections();

        /// <param name="floorHeight">Surface points below this local y are ignored; the pile's underside is hidden by the ground.</param>
        public static bool TryFindSurface(
            VoxelGrid grid,
            Vector3 emberPosition,
            float maxDepth,
            float floorHeight,
            out Vector3 surfacePoint,
            out float depth)
        {
            surfacePoint = default;
            depth = float.MaxValue;

            for (int i = 0; i < ProbeDirections.Length; i++)
            {
                Vector3 direction = ProbeDirections[i];
                if (grid.SampleDensity(emberPosition + direction * maxDepth) >= 0f)
                {
                    continue;
                }

                float crossing = FindCrossing(grid, emberPosition, direction, maxDepth);
                Vector3 candidate = emberPosition + direction * crossing;
                if (crossing >= depth || candidate.y < floorHeight)
                {
                    continue;
                }

                depth = crossing;
                surfacePoint = candidate;
            }

            return depth < float.MaxValue;
        }

        // Binary search for where density drops below zero between the ember (ash) and the probe end (air).
        private static float FindCrossing(VoxelGrid grid, Vector3 origin, Vector3 direction, float maxDistance)
        {
            float inside = 0f;
            float outside = maxDistance;
            for (int step = 0; step < SearchSteps; step++)
            {
                float middle = (inside + outside) * 0.5f;
                if (grid.SampleDensity(origin + direction * middle) >= 0f)
                {
                    inside = middle;
                }
                else
                {
                    outside = middle;
                }
            }

            return (inside + outside) * 0.5f;
        }

        private static Vector3[] CreateProbeDirections()
        {
            Vector3[] directions = new Vector3[14];
            directions[0] = Vector3.up;
            directions[1] = Vector3.down;
            directions[2] = Vector3.left;
            directions[3] = Vector3.right;
            directions[4] = Vector3.forward;
            directions[5] = Vector3.back;

            int index = 6;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        directions[index++] = new Vector3(x, y, z).normalized;
                    }
                }
            }

            return directions;
        }
    }
}
