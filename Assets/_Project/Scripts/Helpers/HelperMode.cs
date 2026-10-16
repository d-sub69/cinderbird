namespace Cinderbird.Helpers
{
    /// <summary>What the player asked a helper to do.</summary>
    public enum HelperMode
    {
        /// <summary>Work the nearest plot with open ash, then the next nearest (the default once hired).</summary>
        Auto = 0,
        /// <summary>Work the plot the player chose; once it is done, carry on as in Auto.</summary>
        Assigned = 1,
        /// <summary>Stop working: deliver what she carries, then wait by the shop.</summary>
        Off = 2,
    }
}
