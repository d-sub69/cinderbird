using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// Where a tool (the basket) may be set down: on solid, fairly flat ground with room around it.
    /// The adapter does the physics queries; this decides, so the rules can be tested.
    /// </summary>
    public static class PlacementRules
    {
        public static PlacementResult Evaluate(
            bool hasSurface,
            Vector3 surfaceNormal,
            float maxSlopeDegrees,
            bool surfaceIsAsh,
            bool inNoPlacementZone,
            bool spaceBlocked)
        {
            if (!hasSurface)
            {
                return PlacementResult.NoSurface;
            }

            // Checked before slope: "not on the ash" is the more useful message on a steep pile.
            if (surfaceIsAsh)
            {
                return PlacementResult.OnAsh;
            }

            if (inNoPlacementZone)
            {
                return PlacementResult.InNoPlacementZone;
            }

            if (Vector3.Angle(surfaceNormal, Vector3.up) > maxSlopeDegrees)
            {
                return PlacementResult.TooSteep;
            }

            return spaceBlocked ? PlacementResult.Blocked : PlacementResult.Valid;
        }
    }
}
