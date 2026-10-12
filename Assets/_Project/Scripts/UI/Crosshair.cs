using Cinderbird.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// Center-screen dot: grey when there's nothing to do, white when you can scoop, pour or pick something up,
    /// and the target's own accent (the nest: orange with a pulsing glow) when it has a <see cref="CrosshairAccent"/>.
    /// </summary>
    public sealed class Crosshair : MonoBehaviour
    {
        [SerializeField] private ScoopInteractor _scoop;
        [Tooltip("Optional: also light up for pour targets (nest, placed basket).")]
        [SerializeField] private PourController _pour;
        [Tooltip("Optional: picks up accents and pick-up targets from what the player looks at.")]
        [SerializeField] private PlayerInteractor _interactor;
        [Tooltip("UI Image (or any Graphic) at the center of the screen.")]
        [SerializeField] private Graphic _dot;
        [Tooltip("Optional soft circle behind the dot, shown for accents with glow.")]
        [SerializeField] private Graphic _halo;
        [SerializeField] private Color _idleColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        [SerializeField] private Color _inReachColor = new Color(1f, 1f, 1f, 0.95f);

        [Header("Glow")]
        [SerializeField, Min(0f)] private float _haloPulsesPerSecond = 1.2f;
        [SerializeField, Range(0f, 1f)] private float _haloMinAlpha = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _haloMaxAlpha = 0.6f;
        [Tooltip("How much bigger the halo gets at the top of a pulse.")]
        [SerializeField, Min(0f)] private float _haloPulseScale = 0.25f;

        private GameObject _accentSource;
        private CrosshairAccent _accent;
        private Color _shownColor;
        private bool _initialized;

        private void Awake()
        {
            if (_scoop == null || _dot == null)
            {
                Debug.LogError($"{nameof(Crosshair)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            if (_halo != null)
            {
                _halo.gameObject.SetActive(false);
            }
        }

        // LateUpdate so it reflects the targets the scoop and interactor found in their Updates this frame.
        private void LateUpdate()
        {
            CrosshairAccent accent = CurrentAccent();
            Color color = accent != null ? accent.Color : HasAction() ? _inReachColor : _idleColor;
            if (!_initialized || color != _shownColor)
            {
                _dot.color = color;
                _shownColor = color;
                _initialized = true;
            }

            UpdateHalo(accent != null && accent.Glow ? accent : null);
        }

        private bool HasAction()
        {
            return (_scoop.isActiveAndEnabled && _scoop.HasTarget) ||
                   (_pour != null && _pour.HasTarget) ||
                   (_interactor != null && _interactor.PickUpTarget != null);
        }

        private CrosshairAccent CurrentAccent()
        {
            GameObject target = _interactor != null ? _interactor.TargetObject : null;
            // Looked up only when the target changes, not every frame.
            if (target != _accentSource)
            {
                _accentSource = target;
                _accent = target != null ? target.GetComponentInParent<CrosshairAccent>() : null;
            }

            return _accent;
        }

        private void UpdateHalo(CrosshairAccent glowing)
        {
            if (_halo == null)
            {
                return;
            }

            bool visible = glowing != null;
            if (_halo.gameObject.activeSelf != visible)
            {
                _halo.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            float wave = Mathf.Sin(Time.unscaledTime * _haloPulsesPerSecond * Mathf.PI * 2f) * 0.5f + 0.5f;
            Color haloColor = glowing.Color;
            haloColor.a = Mathf.Lerp(_haloMinAlpha, _haloMaxAlpha, wave);
            _halo.color = haloColor;
            _halo.rectTransform.localScale = Vector3.one * (1f + _haloPulseScale * wave);
        }
    }
}
