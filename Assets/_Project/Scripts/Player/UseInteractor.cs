using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Player
{
    /// <summary>
    /// Thin adapter: the use key (E) uses whatever <see cref="PlayerInteractor"/> says the player looks at (the shop
    /// stall). Shares the key with picking up a placed tool; a collider is only ever one of the two.
    /// </summary>
    public sealed class UseInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private InputActionReference _useAction;

        private void Awake()
        {
            if (_interactor == null || _useAction == null)
            {
                Debug.LogError($"{nameof(UseInteractor)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _useAction.action.Enable();
        }

        private void Update()
        {
            // While the cursor is free (paused, in a menu) the key belongs to the menu.
            if (Cursor.lockState != CursorLockMode.Locked || !_useAction.action.WasPressedThisFrame())
            {
                return;
            }

            _interactor.UseTarget?.Interact();
        }
    }
}
