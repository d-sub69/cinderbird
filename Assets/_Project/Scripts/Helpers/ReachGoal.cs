using UnityEngine;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// A path goal "stand somewhere from where the target is in reach": within a ring around it (not on top of it, not too
    /// far) and at a height from which it is neither too high nor too low. Reused between searches.
    /// </summary>
    public sealed class ReachGoal : IPathGoal
    {
        private NavGrid _grid;
        private Vector3 _target;
        private float _minDistance;
        private float _maxDistance;
        private float _lowest;
        private float _highest;

        public Vector3 Target => _target;

        /// <param name="target">World position to reach.</param>
        /// <param name="reachUp">The target may be this much above where she stands.</param>
        /// <param name="reachDown">The target may be this much below where she stands.</param>
        public void Set(NavGrid grid, Vector3 target, float minDistance, float maxDistance, float reachUp, float reachDown)
        {
            _grid = grid;
            _target = target;
            _minDistance = Mathf.Max(0f, minDistance);
            _maxDistance = Mathf.Max(_minDistance, maxDistance);
            _lowest = target.y - reachUp;
            _highest = target.y + reachDown;
        }

        public bool IsGoal(int x, int z)
        {
            if (!_grid.IsWalkable(x, z))
            {
                return false;
            }

            Vector3 center = _grid.CenterOf(x, z);
            float distance = HorizontalDistance(center);
            return distance >= _minDistance && distance <= _maxDistance && center.y >= _lowest && center.y <= _highest;
        }

        /// <summary>Whether a world position (where she stands) is a goal, without snapping to a cell.</summary>
        public bool Contains(Vector3 position)
        {
            float distance = HorizontalDistance(position);
            return distance >= _minDistance && distance <= _maxDistance && position.y >= _lowest - 0.2f && position.y <= _highest + 0.2f;
        }

        public float Estimate(int x, int z)
        {
            return Mathf.Max(0f, HorizontalDistance(_grid.CenterOf(x, z)) - _maxDistance) / _grid.CellSize;
        }

        private float HorizontalDistance(Vector3 point)
        {
            float dx = point.x - _target.x;
            float dz = point.z - _target.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
