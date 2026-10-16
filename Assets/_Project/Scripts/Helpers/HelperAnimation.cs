using UnityEngine;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// A helper's body animation, as her logic drives it: a pose, how fast she walks, and the scoop's reach to the ash.
    /// The placeholder body animates procedurally (<see cref="HelperPrimitiveAnimation"/>); the real model will drive an
    /// Animator from the same calls, so nothing in her logic changes when the art arrives.
    /// </summary>
    public abstract class HelperAnimation : MonoBehaviour
    {
        public abstract void SetPose(HelperPose pose);

        /// <param name="speed">Horizontal speed in m/s.</param>
        public abstract void SetWalkSpeed(float speed);

        /// <summary>Reaches the sieve down to a point and back over the scoop's pour time.</summary>
        public abstract void Scoop(Vector3 worldPoint, float seconds);
    }
}
