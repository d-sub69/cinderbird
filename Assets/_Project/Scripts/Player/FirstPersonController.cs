using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Player
{
    /// <summary>
    /// Thin adapter: reads input actions, drives <see cref="FirstPersonMotor"/> and
    /// <see cref="LookRotation"/>, and applies the results to the CharacterController and camera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private PlayerMovementSettings _settings;
        [Tooltip("Child transform that pitches up/down; usually the camera.")]
        [SerializeField] private Transform _cameraPivot;

        [Header("Input")]
        [SerializeField] private InputActionReference _moveAction;
        [SerializeField] private InputActionReference _lookAction;
        [SerializeField] private InputActionReference _jumpAction;
        [Tooltip("Optional. While this action is held the camera stops turning, e.g. so mouse movement can shake the sieve.")]
        [SerializeField] private InputActionReference _suspendLookAction;

        private CharacterController _characterController;
        private FirstPersonMotor _motor;
        private LookRotation _look;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            if (_settings == null || _cameraPivot == null || _moveAction == null || _lookAction == null || _jumpAction == null)
            {
                Debug.LogError($"{nameof(FirstPersonController)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _motor = new FirstPersonMotor(_settings.ToMotorConfig());
            _look = new LookRotation(_settings.MaxPitch, transform.eulerAngles.y);
        }

        private void OnEnable()
        {
            // Null when Awake bailed out on missing references and someone re-enables us.
            if (_motor == null)
            {
                return;
            }

            _moveAction.action.Enable();
            _lookAction.action.Enable();
            _jumpAction.action.Enable();
            if (_suspendLookAction != null)
            {
                _suspendLookAction.action.Enable();
            }

            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            if (_motor == null)
            {
                return;
            }

            _moveAction.action.Disable();
            _lookAction.action.Disable();
            _jumpAction.action.Disable();
            SetCursorLocked(false);
        }

        private void Update()
        {
            UpdateCursorLock();
            UpdateLook();
            UpdateMovement();
        }

        private void UpdateLook()
        {
            // Without the lock check, moving the mouse to click Editor buttons would spin the camera.
            bool lookSuspended = _suspendLookAction != null && _suspendLookAction.action.IsPressed();
            if (Cursor.lockState == CursorLockMode.Locked && !lookSuspended)
            {
                _look.Apply(_lookAction.action.ReadValue<Vector2>(), _settings.LookSensitivity);
            }

            transform.localRotation = Quaternion.Euler(0f, _look.Yaw, 0f);
            _cameraPivot.localRotation = Quaternion.Euler(_look.Pitch, 0f, 0f);
        }

        private void UpdateMovement()
        {
            if (_jumpAction.action.WasPressedThisFrame())
            {
                _motor.RequestJump();
            }

            Vector2 moveInput = _moveAction.action.ReadValue<Vector2>();
            Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.y;

            float deltaTime = Time.deltaTime;
            Vector3 velocity = _motor.Tick(direction, _characterController.isGrounded, deltaTime);
            CollisionFlags flags = _characterController.Move(velocity * deltaTime);

            if ((flags & CollisionFlags.Above) != 0)
            {
                _motor.NotifyHitCeiling();
            }
        }

        // Debug-level cursor handling; a proper pause menu will replace this later.
        private static void UpdateCursorLock()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
            else if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
