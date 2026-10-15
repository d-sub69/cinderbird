namespace Cinderbird.Core
{
    public enum GamePhase
    {
        /// <summary>The bootstrap is building the mountain and loading the save; the game is frozen.</summary>
        Loading,
        Playing,
        /// <summary>The pause menu is open; the game is frozen and the cursor is free.</summary>
        Paused,
        /// <summary>A menu in the world is open (e.g. the shop): the cursor is free but the game keeps running.</summary>
        InMenu,
    }
}
