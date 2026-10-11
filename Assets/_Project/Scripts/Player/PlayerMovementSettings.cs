using UnityEngine;

namespace Cinderbird.Player
{
    [CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "Cinderbird/Player Movement Settings")]
    public sealed class PlayerMovementSettings : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float _walkSpeed = 4f;
        [SerializeField, Min(0f)] private float _groundAcceleration = 40f;
        [SerializeField, Min(0f)] private float _airAcceleration = 10f;

        [Header("Jump")]
        [SerializeField, Min(0f)] private float _jumpHeight = 1f;
        [SerializeField, Min(0.01f)] private float _gravity = 20f;
        [Tooltip("Seconds after walking off a ledge during which a jump still works.")]
        [SerializeField, Min(0f)] private float _coyoteTime = 0.1f;
        [Tooltip("Seconds a jump press is remembered before landing.")]
        [SerializeField, Min(0f)] private float _jumpBufferTime = 0.1f;

        [Header("Look")]
        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [SerializeField, Min(0f)] private float _lookSensitivity = 0.1f;
        [SerializeField, Range(0f, 89f)] private float _maxPitch = 85f;

        public float LookSensitivity => _lookSensitivity;
        public float MaxPitch => _maxPitch;

        public MotorConfig ToMotorConfig()
        {
            return new MotorConfig(
                _walkSpeed,
                _groundAcceleration,
                _airAcceleration,
                _jumpHeight,
                _gravity,
                _coyoteTime,
                _jumpBufferTime);
        }
    }
}
