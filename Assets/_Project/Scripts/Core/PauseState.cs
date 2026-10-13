using System;

namespace Cinderbird.Core
{
    /// <summary>
    /// Which phase the game is in and the allowed moves between them: Loading → Playing once, then
    /// Playing ↔ Paused. Plain C# so the rules can be tested without a scene.
    /// </summary>
    public sealed class PauseState
    {
        public event Action<GamePhase> Changed;

        public GamePhase Phase { get; private set; } = GamePhase.Loading;

        /// <summary>Whether the game world should stand still (loading or paused).</summary>
        public bool IsFrozen => Phase != GamePhase.Playing;

        /// <returns>False if loading had already finished.</returns>
        public bool FinishLoading()
        {
            return MoveFrom(GamePhase.Loading, GamePhase.Playing);
        }

        /// <returns>False unless the game was being played.</returns>
        public bool Pause()
        {
            return MoveFrom(GamePhase.Playing, GamePhase.Paused);
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
