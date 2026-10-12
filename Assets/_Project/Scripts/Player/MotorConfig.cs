namespace Cinderbird.Player
{
    /// <summary>
    /// Plain-data snapshot of movement tuning, so <see cref="FirstPersonMotor"/> can be built
    /// in tests without a ScriptableObject.
    /// </summary>
    public readonly struct MotorConfig
    {
        public readonly float WalkSpeed;
        public readonly float GroundAcceleration;
        public readonly float AirAcceleration;
        public readonly float JumpHeight;
        public readonly float Gravity;
        public readonly float CoyoteTime;
        public readonly float JumpBufferTime;
        public readonly float SprintMultiplier;
        public readonly float CrouchMultiplier;

        public MotorConfig(
            float walkSpeed,
            float groundAcceleration,
            float airAcceleration,
            float jumpHeight,
            float gravity,
            float coyoteTime,
            float jumpBufferTime,
            float sprintMultiplier = 1f,
            float crouchMultiplier = 1f)
        {
            WalkSpeed = walkSpeed;
            GroundAcceleration = groundAcceleration;
            AirAcceleration = airAcceleration;
            JumpHeight = jumpHeight;
            Gravity = gravity;
            CoyoteTime = coyoteTime;
            JumpBufferTime = jumpBufferTime;
            SprintMultiplier = sprintMultiplier;
            CrouchMultiplier = crouchMultiplier;
        }
    }
}
