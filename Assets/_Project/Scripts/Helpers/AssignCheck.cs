namespace Cinderbird.Helpers
{
    /// <summary>Whether a helper can be sent to a spot the player points at, and if not, why.</summary>
    public enum AssignCheck
    {
        /// <summary>She can go there: on open ash (she works that layer), or on the ground near the ash.</summary>
        Valid,
        /// <summary>Not pointing at anything in reach.</summary>
        NoSurface,
        /// <summary>The ash there is a sealed layer.</summary>
        Sealed,
        /// <summary>On the ground, but too far from the ash.</summary>
        NoAshNearby,
        /// <summary>She can't walk there (too steep, or walled in).</summary>
        Unreachable,
    }
}
