using UnityEngine;

namespace Cinderbird.Core
{
    /// <summary>
    /// How long this game has been played, across sessions (saved with the game). Game time only: the pause menu stops
    /// it, as it stops the rest of the game. For pacing logs now, and the credits' "you played 3 h 40 min" later.
    /// </summary>
    public sealed class PlayClock : MonoBehaviour
    {
        /// <summary>Seconds played in this game so far.</summary>
        public double Seconds { get; private set; }

        /// <summary>Sets the time already played, from a save (0 for a new game).</summary>
        public void Restore(double seconds)
        {
            Seconds = seconds > 0.0 ? seconds : 0.0;
        }

        private void Update()
        {
            Seconds += Time.deltaTime;
        }
    }
}
