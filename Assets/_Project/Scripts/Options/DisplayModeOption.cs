namespace Cinderbird.Options
{
    /// <summary>How the game window is shown. Stored by number in options.json, so only add values at the end.</summary>
    public enum DisplayModeOption
    {
        /// <summary>A window covering the whole screen without borders; quick to Alt-Tab.</summary>
        Borderless = 0,
        /// <summary>Exclusive fullscreen; can change the monitor's resolution.</summary>
        Fullscreen = 1,
        Windowed = 2,
    }
}
