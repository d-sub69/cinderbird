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
        [Tooltip("Optional. Hold to sprint.")]
        [SerializeField] private InputActionReference _sprintAction;
        [Tooltip("Optional. Hold to crouch.")]
        [SerializeField] private InputActionReference _crouchAction;

        // Keeps the headroom probe slightly narrower than the capsule so walls beside us don't count.
        private const float HeadroomProbeShrink = 0.9f;

        private CharacterController _characterController;
        private FirstPersonMotor _motor;
        private LookRotation _look;
        private CrouchState _crouch;
        private float _standHeight;
        private float _capsuleBottom;
        private float _eyeBelowTop;

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
            SetUpCrouch();
        }

        // Measures the standing pose as set up in the Editor, so crouching only needs a target height.
        private void SetUpCrouch()
        {
            _standHeight = _characterController.height;
            _capsuleBottom = _characterController.center.y - _standHeight * 0.5f;
            _eyeBelowTop = _capsuleBottom + _standHeight - _cameraPivot.localPosition.y;

            // A CharacterController shorter than its diameter becomes a sphere and misbehaves.
            float minHeight = _characterController.radius * 2f + 0.01f;
            float crouchHeight = Mathf.Clamp(_settings.CrouchHeight, minHeight, _standHeight);
            _crouch = new CrouchState(_standHeight, crouchHeight, _settings.CrouchTransitionSpeed);
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
            EnableOptional(_suspendLookAction);
            EnableOptional(_sprintAction);
            EnableOptional(_crouchAction);

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
            UpdateCrouch();
            UpdateMovement();
        }

        private void UpdateCrouch()
        {
            bool wantsCrouch = IsHeld(_crouchAction);
            float heightBefore = _crouch.Height;
            bool canStand = heightBefore >= _standHeight || HasHeadroom(heightBefore);
            _crouch.Tick(wantsCrouch, canStand, Time.deltaTime);

            if (!Mathf.Approximately(_crouch.Height, heightBefore))
            {
                ApplyHeight(_crouch.Height);
            }
        }

        // Shrinks or grows the capsule from the top, keeping the feet where they are.
        private void ApplyHeight(float height)
        {
            Vector3 center = _characterController.center;
            _characterController.height = height;
            _characterController.center = new Vector3(center.x, _capsuleBottom + height * 0.5f, center.z);

            Vector3 eye = _cameraPivot.localPosition;
            _cameraPivot.localPosition = new Vector3(eye.x, _capsuleBottom + height - _eyeBelowTop, eye.z);
        }

        private bool HasHeadroom(float currentHeight)
        {
            float radius = _characterController.radius;
            // Start inside our own capsule's top sphere; casts ignore colliders they start inside, including ours.
            Vector3 topSphere = transform.TransformPoint(new Vector3(0f, _capsuleBottom + currentHeight - radius, 0f));
            float distance = _standHeight - currentHeight + _characterController.skinWidth;
            return !Physics.SphereCast(topSphere, radius * HeadroomProbeShrink, Vector3.up, out _, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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

            _motor.SetStance(IsHeld(_sprintAction), _crouch.IsCrouching);

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

        private static bool IsHeld(InputActionReference action)
        {
            return action != null && action.action.IsPressed();
        }

        private static void EnableOptional(InputActionReference action)
        {
            if (action != null)
            {
                action.action.Enable();
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
