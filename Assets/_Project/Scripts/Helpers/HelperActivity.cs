namespace Cinderbird.Helpers
{
    /// <summary>What a helper does next, as <see cref="HelperBrain"/> decides it.</summary>
    public enum HelperActivity
    {
        /// <summary>Stand by the shop.</summary>
        Idle,
        /// <summary>Hold still: talking to the player, or a scoop or pour still playing.</summary>
        Wait,
        GoHome,
        /// <summary>Walk to the spot the player sent her to; she starts work from there.</summary>
        GoToSpot,
        GoToWork,
        Scoop,
        Shake,
        GoToNest,
        Pour,
    }
}
