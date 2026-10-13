using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Testing aid: a key (F4) opens the next ash layer. In the full game layers open with Heat levels.
    /// </summary>
    [RequireComponent(typeof(VoxelTerrain))]
    public sealed class DebugLayerUnlocker : MonoBehaviour
    {
        [SerializeField] private InputActionReference _unlockAction;

        private VoxelTerrain _terrain;

        private void Awake()
        {
            _terrain = GetComponent<VoxelTerrain>();
            if (_unlockAction == null)
            {
                Debug.LogError($"{nameof(DebugLayerUnlocker)} on '{name}' has no unlock action.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_unlockAction != null)
            {
                _unlockAction.action.Enable();
            }
        }

        private void Update()
        {
            if (_unlockAction.action.WasPressedThisFrame())
            {
                _terrain.UnlockNextLayer();
            }
        }
    }
}
