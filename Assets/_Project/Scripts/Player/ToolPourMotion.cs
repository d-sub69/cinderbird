using Cinderbird.Embers;
using Cinderbird.Sifting;
using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// Tilts a held tool forward when it's poured, or wobbles it side to side when a pour is refused.
    /// Rotates its own pivot (placed at the tool's front rim) so it never fights other motion on the tool.
    /// The player's tools play it on their <see cref="PourController"/>'s pours; a helper's sieve has no controller and
    /// plays it from code (<see cref="Play"/>).
    /// </summary>
    public sealed class ToolPourMotion : MonoBehaviour
    {
        [Tooltip("Optional: plays on this controller's pours. Empty for a tool played from code (a helper's).")]
        [SerializeField] private PourController _controller;
        [Tooltip("Only pours of this tool animate this object.")]
        [SerializeField] private ToolDefinition _tool;
        [SerializeField] private TipSettings _settings;
        [Tooltip("Child at the tool's front rim, parenting its visible parts.")]
        [SerializeField] private Transform _pivot;

        private Quaternion _restRotation;
        private float _elapsed = -1f;
        private bool _refusing;
        private float _timeScale = 1f;

        /// <summary>Seconds a pour's tip takes at the current time scale.</summary>
        public float TipDuration => _settings != null ? _settings.TipDuration / _timeScale : 0f;

        public bool IsPlaying => _elapsed >= 0f;

        private void Awake()
        {
            if (_settings == null || _pivot == null)
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
            if (_controller != null)
            {
                _controller.Poured -= OnPoured;
                _controller.PourRefused -= OnPourRefused;
            }

            // Stowed mid-animation: come back upright next time it's drawn.
            if (_pivot != null && _elapsed >= 0f)
            {
                _pivot.localRotation = _restRotation;
                _elapsed = -1f;
            }
        }

        /// <summary>Plays the tip (or, when <paramref name="refusing"/>, the "no" wobble) from the start.</summary>
        public void Play(bool refusing)
        {
            _refusing = refusing;
            _elapsed = 0f;
        }

        /// <summary>Plays faster (above 1) or slower: a helper's pace upgrade shortens her pour.</summary>
        public void SetTimeScale(float scale)
        {
            _timeScale = Mathf.Max(0.01f, scale);
        }

        private void LateUpdate()
        {
            if (_elapsed < 0f)
            {
                return;
            }

            _elapsed += Time.deltaTime * _timeScale;
            float duration = _refusing ? _settings.RefuseDuration : _settings.TipDuration;
            float progress = _elapsed / duration;
            if (progress >= 1f)
            {
                _pivot.localRotation = _restRotation;
                _elapsed = -1f;
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
    }
}
