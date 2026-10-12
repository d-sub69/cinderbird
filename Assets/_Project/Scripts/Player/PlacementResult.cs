namespace Cinderbird.Player
{
    /// <summary>Whether a tool can be set down where the player is looking, and if not, why.</summary>
    public enum PlacementResult
    {
        Valid,

        /// <summary>Nothing within reach to put it on.</summary>
        NoSurface,

        /// <summary>The ash pile shifts as you dig, so tools only go on solid ground.</summary>
        OnAsh,

        /// <summary>Inside an area marked off-limits, such as the nest.</summary>
        InNoPlacementZone,

        TooSteep,

        /// <summary>Something (a wall, the player) is in the way.</summary>
        Blocked,
    }
}
