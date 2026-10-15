using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// What a small falling thing lying on the pile (a loose ember, an uncovered offering) can land on: ash, read from the
    /// density field (up to date at once), any collider below on the ground mask, and never through the mountain's floor.
    /// Positions are in the pile's local space.
    /// </summary>
    public sealed class AshFallSurface : IFallSurface
    {
        private readonly VoxelTerrain _pile;
        private readonly LayerMask _groundMask;

        public AshFallSurface(VoxelTerrain pile, LayerMask groundMask)
        {
            _pile = pile;
            _groundMask = groundMask;
        }

        public bool TryLand(Vector3 center, float drop, float radius, out Vector3 landed)
        {
            Transform pile = _pile.transform;
            Vector3 world = pile.TransformPoint(center);
            if (LooseEmberRules.RestsOnAsh(_pile.Terrain.World, pile.InverseTransformPoint(world + Vector3.down * drop), radius))
            {
                landed = pile.InverseTransformPoint(world + Vector3.down * drop);
                return true;
            }

            if (Physics.Raycast(world, Vector3.down, out RaycastHit hit, drop + radius, _groundMask, QueryTriggerInteraction.Ignore))
            {
                landed = pile.InverseTransformPoint(hit.point + Vector3.up * radius);
                return true;
            }

            float floor = pile.TransformPoint(new Vector3(0f, _pile.LocalFloorHeight, 0f)).y;
            if (world.y - drop - radius <= floor)
            {
                landed = pile.InverseTransformPoint(new Vector3(world.x, floor + radius, world.z));
                return true;
            }

            landed = center;
            return false;
        }

        /// <summary>Ground or rocks right under it (not ash: that is the density field's job).</summary>
        public bool IsOnGround(Vector3 center, float radius)
        {
            Vector3 world = _pile.transform.TransformPoint(center);
            return Physics.Raycast(world, Vector3.down, radius + 0.05f, _groundMask, QueryTriggerInteraction.Ignore);
        }
    }
}
