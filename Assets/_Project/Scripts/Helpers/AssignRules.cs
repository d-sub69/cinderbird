using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// Where the player may send a helper, and which layer she then works. On the ash: anywhere its top layer is open, and
    /// she works that layer. On the ground: within a few meters of the pile, and she works the outermost open layer. Whether
    /// she can walk there is checked separately (it needs a path search). Local space.
    /// </summary>
    public static class AssignRules
    {
        /// <summary>The layer for a spot on the ground: the outermost open layer that still has ash.</summary>
        public const int OutermostLayer = -1;

        /// <param name="point">The spot pointed at (local).</param>
        /// <param name="onAsh">Whether the spot is on the pile's surface (rather than the ground or an object).</param>
        /// <param name="maxDistance">On the ground, how far from the ash the spot may be (meters).</param>
        /// <param name="stride">Meters between the columns checked around a spot on the ground.</param>
        /// <param name="layer">The layer she would work: the one under the spot, or <see cref="OutermostLayer"/>.</param>
        public static AssignCheck Check(VoxelWorld world, LayerLocks locks, Vector3 point, bool onAsh, float maxDistance, float stride,
            out int layer)
        {
            layer = OutermostLayer;
            if (onAsh)
            {
                // Any depth counts here: she starts at the spot and finds the nearest ash of its layer from there.
                if (AshSurvey.TryOpenAsh(world, locks, point.x, point.z, 0f, out _, out _, out int topLayer))
                {
                    layer = topLayer;
                    return AssignCheck.Valid;
                }

                return topLayer >= 0 ? AssignCheck.Sealed : AssignCheck.NoAshNearby;
            }

            return HasAshWithin(world, point, maxDistance, stride) ? AssignCheck.Valid : AssignCheck.NoAshNearby;
        }

        // Any ash at all, sealed or not: the ground spot only has to be beside the pile.
        private static bool HasAshWithin(VoxelWorld world, Vector3 point, float maxDistance, float stride)
        {
            stride = Mathf.Max(0.1f, stride);
            int steps = Mathf.CeilToInt(maxDistance / stride);
            for (int j = -steps; j <= steps; j++)
            {
                for (int i = -steps; i <= steps; i++)
                {
                    float dx = i * stride;
                    float dz = j * stride;
                    float x = point.x + dx;
                    float z = point.z + dz;
                    // Outside the grid a column lookup would clamp to its edge and find ash that isn't there.
                    if (dx * dx + dz * dz > maxDistance * maxDistance || x < world.Origin.x || z < world.Origin.z ||
                        x > world.Origin.x + world.Size.x || z > world.Origin.z + world.Size.z)
                    {
                        continue;
                    }

                    if (world.TryFindTop(x, z, out float top) && top > world.Origin.y + world.CellSize * 1.5f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
