using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Owns the sieve's <see cref="SieveContents"/> so other components can reach it through the Inspector.
    /// </summary>
    public sealed class Sieve : MonoBehaviour
    {
        [Tooltip("Step 3 placeholder: pressing this instantly empties the ash. Step 4 replaces it with real shaking.")]
        [SerializeField] private InputActionReference _debugEmptyAshAction;

        public SieveContents Contents { get; } = new SieveContents();

        private void OnEnable()
        {
            if (_debugEmptyAshAction != null)
            {
                _debugEmptyAshAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (_debugEmptyAshAction != null)
            {
                _debugEmptyAshAction.action.Disable();
            }
        }

        private void Update()
        {
            if (_debugEmptyAshAction != null && _debugEmptyAshAction.action.WasPressedThisFrame())
            {
                Contents.RemoveAsh(Contents.AshVolume);
            }
        }
    }
}
