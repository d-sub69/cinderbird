using System;

namespace Cinderbird.Core
{
    /// <summary>
    /// Which phase the game is in and the allowed moves between them: Loading → Playing once, then
    /// Playing ↔ Paused and Playing ↔ InMenu; pausing from a menu closes it (resuming goes back to Playing).
    /// Plain C# so the rules can be tested without a scene.
    /// </summary>
    public sealed class PauseState
    {
        public event Action<GamePhase> Changed;

        public GamePhase Phase { get; private set; } = GamePhase.Loading;

        /// <summary>Whether the game world should stand still (loading or paused).</summary>
        public bool IsFrozen => Phase == GamePhase.Loading || Phase == GamePhase.Paused;

        /// <returns>False if loading had already finished.</returns>
        public bool FinishLoading()
        {
            return MoveFrom(GamePhase.Loading, GamePhase.Playing);
        }

        /// <returns>False unless the game was being played (or a menu in the world was open).</returns>
        public bool Pause()
        {
            return MoveFrom(GamePhase.Playing, GamePhase.Paused) || MoveFrom(GamePhase.InMenu, GamePhase.Paused);
        }

        /// <summary>Opens a menu in the world (the shop) while the game keeps running.</summary>
        /// <returns>False unless the game was being played.</returns>
        public bool OpenMenu()
        {
            return MoveFrom(GamePhase.Playing, GamePhase.InMenu);
        }

        /// <returns>False unless a menu in the world was open.</returns>
        public bool CloseMenu()
        {
            return MoveFrom(GamePhase.InMenu, GamePhase.Playing);
        }

        /// <returns>False unless the game was paused.</returns>
        public bool Resume()
        {
            return MoveFrom(GamePhase.Paused, GamePhase.Playing);
        }

        private bool MoveFrom(GamePhase from, GamePhase to)
        {
            if (Phase != from)
            {
                return false;
            }

            Phase = to;
            Changed?.Invoke(to);
            return true;
        }
    }
}
