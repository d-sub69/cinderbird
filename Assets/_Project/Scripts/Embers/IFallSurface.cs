using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>What a falling loose ember can land on: ash, the ground, rocks. Positions are in the pile's local space.</summary>
    public interface IFallSurface
    {
        /// <summary>Whether a ball dropping <paramref name="drop"/> meters from <paramref name="center"/> lands on something.</summary>
        /// <param name="landed">Where its center comes to rest, resting on the surface.</param>
        bool TryLand(Vector3 center, float drop, float radius, out Vector3 landed);
    }
}
