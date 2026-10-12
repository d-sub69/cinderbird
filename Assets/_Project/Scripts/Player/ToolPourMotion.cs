using Cinderbird.Embers;
using Cinderbird.Sifting;
using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// Tilts a held tool forward when it's poured, or wobbles it side to side when a pour is refused.
    /// Rotates its own pivot (placed at the tool's front rim) so it never fights other motion on the tool.
    /// </summary>
    public sealed class ToolPourMotion : MonoBehaviour
    {
        [SerializeField] private PourController _controller;
        [Tooltip("Only pours of this tool animate this object.")]
        [SerializeField] private ToolDefinition _tool;
        [SerializeField] private TipSettings _settings;
        [Tooltip("Child at the tool's front rim, parenting its visible parts.")]
        [SerializeField] private Transform _pivot;

        private Quaternion _restRotation;
        private float _startTime = -1f;
        private bool _refusing;

        private void Awake()
        {
            if (_controller == null || _tool == null || _settings == null || _pivot == null)
            {
                Debug.LogError($"{nameof(ToolPourMotion)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _restRotation = _pivot.localRotation;
        }

        private void OnEnable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.Poured += OnPoured;
            _controller.PourRefused += OnPourRefused;
        }

        private void OnDisable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.Poured -= OnPoured;
            _controller.PourRefused -= OnPourRefused;

            // Stowed mid-animation: come back upright next time it's drawn.
            if (_pivot != null && _startTime >= 0f)
            {
                _pivot.localRotation = _restRotation;
                _startTime = -1f;
            }
        }

        private void LateUpdate()
        {
            if (_startTime < 0f)
            {
                return;
            }

            float duration = _refusing ? _settings.RefuseDuration : _settings.TipDuration;
            float progress = (Time.time - _startTime) / duration;
            if (progress >= 1f)
            {
                _pivot.localRotation = _restRotation;
                _startTime = -1f;
                return;
            }

            _pivot.localRotation = _restRotation * CurrentTilt(progress);
        }

        private Quaternion CurrentTilt(float progress)
        {
            if (_refusing)
            {
                // Two quick side-to-side "no" shakes that die out.
                float wobble = _settings.RefuseAngle * Mathf.Sin(progress * Mathf.PI * 4f) * (1f - progress);
                return Quaternion.Euler(0f, 0f, wobble);
            }

            // Front edge dips toward the target and comes back in one smooth arc.
            return Quaternion.Euler(_settings.TipAngle * Mathf.Sin(progress * Mathf.PI), 0f, 0f);
        }

        private void OnPoured(ToolDefinition tool, IPourTarget target, int moved)
        {
            if (tool == _tool)
            {
                Play(false);
            }
        }

        private void OnPourRefused(ToolDefinition tool, PourOutcome outcome)
        {
            if (tool == _tool)
            {
                Play(true);
            }
        }

        private void Play(bool refusing)
        {
            _refusing = refusing;
            _startTime = Time.time;
        }
    }
}
