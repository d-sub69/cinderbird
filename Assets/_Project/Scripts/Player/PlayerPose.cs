using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>Where the player stands and looks, as plain data for saving.</summary>
    public readonly struct PlayerPose
    {
        public PlayerPose(Vector3 position, float yaw, float pitch, bool crouching)
        {
            Position = position;
            Yaw = yaw;
            Pitch = pitch;
            Crouching = crouching;
        }

        /// <summary>World position of the player object (the feet end of the capsule).</summary>
        public Vector3 Position { get; }

        /// <summary>Body facing in degrees around the up axis.</summary>
        public float Yaw { get; }

        /// <summary>Camera tilt in degrees; negative looks up.</summary>
        public float Pitch { get; }

        public bool Crouching { get; }
    }
}
