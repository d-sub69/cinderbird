using Cinderbird.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// Center-screen dot: grey when there's nothing to do, white when the pile is within scoop reach
    /// or you're looking at something you can pour into.
    /// </summary>
    public sealed class Crosshair : MonoBehaviour
    {
        [SerializeField] private ScoopInteractor _scoop;
        [Tooltip("Optional: also light up for pour targets (nest, placed basket).")]
        [SerializeField] private PourController _pour;
        [Tooltip("UI Image (or any Graphic) at the center of the screen.")]
        [SerializeField] private Graphic _dot;
        [SerializeField] private Color _idleColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        [SerializeField] private Color _inReachColor = new Color(1f, 1f, 1f, 0.95f);

        private bool _initialized;
        private bool _shownInReach;

        private void Awake()
        {
            if (_scoop == null || _dot == null)
            {
                Debug.LogError($"{nameof(Crosshair)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        // LateUpdate so it reflects the target the scoop found in its Update this frame.
        private void LateUpdate()
        {
            bool inReach = (_scoop.isActiveAndEnabled && _scoop.HasTarget) || (_pour != null && _pour.HasTarget);
            if (_initialized && inReach == _shownInReach)
            {
                return;
            }

            _dot.color = inReach ? _inReachColor : _idleColor;
            _shownInReach = inReach;
            _initialized = true;
        }
    }
}
