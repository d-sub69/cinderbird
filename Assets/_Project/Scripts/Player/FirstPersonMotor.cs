using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// Pure movement logic: turns a desired direction and grounded state into a velocity.
    /// Knows nothing about CharacterController or input, so it can be unit tested.
    /// </summary>
    public sealed class FirstPersonMotor
    {
        // A small constant downward push while grounded keeps CharacterController.isGrounded
        // stable on slopes and steps; with zero it flickers every other frame.
        private const float GroundedStickVelocity = -2f;

        private readonly MotorConfig _config;
        private Vector3 _velocity;
        private float _timeSinceGrounded = float.PositiveInfinity;
        private float _timeSinceJumpRequested = float.PositiveInfinity;

        public FirstPersonMotor(MotorConfig config)
        {
            _config = config;
        }

        public Vector3 Velocity => _velocity;

        public void RequestJump()
        {
            _timeSinceJumpRequested = 0f;
        }

        public void NotifyHitCeiling()
        {
            if (_velocity.y > 0f)
            {
                _velocity.y = 0f;
            }
        }

        /// <param name="desiredDirection">World-space horizontal direction; magnitude above 1 is clamped.</param>
        /// <returns>The velocity to move with this frame.</returns>
        public Vector3 Tick(Vector3 desiredDirection, bool isGrounded, float deltaTime)
        {
            UpdateGrounding(isGrounded, deltaTime);
            UpdateHorizontal(desiredDirection, isGrounded, deltaTime);
            TryJump();

            if (!isGrounded)
            {
                _velocity.y -= _config.Gravity * deltaTime;
            }

            _timeSinceJumpRequested += deltaTime;
            return _velocity;
        }

        private void UpdateGrounding(bool isGrounded, float deltaTime)
        {
            if (!isGrounded)
            {
                _timeSinceGrounded += deltaTime;
                return;
            }

            // Only count as landed when not still rising from a jump; the controller can
            // report grounded on the frame right after takeoff.
            if (_velocity.y <= 0f)
            {
                _timeSinceGrounded = 0f;
                _velocity.y = GroundedStickVelocity;
            }
        }

        private void UpdateHorizontal(Vector3 desiredDirection, bool isGrounded, float deltaTime)
        {
            desiredDirection.y = 0f;
            Vector3 target = Vector3.ClampMagnitude(desiredDirection, 1f) * _config.WalkSpeed;
            float acceleration = isGrounded ? _config.GroundAcceleration : _config.AirAcceleration;

            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, target, acceleration * deltaTime);

            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
        }

        private void TryJump()
        {
            bool jumpBuffered = _timeSinceJumpRequested <= _config.JumpBufferTime;
            bool canJump = _timeSinceGrounded <= _config.CoyoteTime;
            if (!jumpBuffered || !canJump)
            {
                return;
            }

            _velocity.y = Mathf.Sqrt(2f * _config.Gravity * _config.JumpHeight);
            // Consume both windows so one press gives exactly one jump.
            _timeSinceJumpRequested = float.PositiveInfinity;
            _timeSinceGrounded = float.PositiveInfinity;
        }
    }
}
