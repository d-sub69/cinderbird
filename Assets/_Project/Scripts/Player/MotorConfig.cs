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

        public MotorConfig(
            float walkSpeed,
            float groundAcceleration,
            float airAcceleration,
            float jumpHeight,
            float gravity,
            float coyoteTime,
            float jumpBufferTime)
        {
            WalkSpeed = walkSpeed;
            GroundAcceleration = groundAcceleration;
            AirAcceleration = airAcceleration;
            JumpHeight = jumpHeight;
            Gravity = gravity;
            CoyoteTime = coyoteTime;
            JumpBufferTime = jumpBufferTime;
        }
    }
}
