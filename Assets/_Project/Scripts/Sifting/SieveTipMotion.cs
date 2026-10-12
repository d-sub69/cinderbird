using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Rolls the sieve's tip pivot toward the basket and back on a tip, or wobbles it on a refused tip.
    /// Works on its own child pivot so it never fights <see cref="SieveShakeMotion"/>, which moves the parent.
    /// </summary>
    public sealed class SieveTipMotion : MonoBehaviour
    {
        [SerializeField] private SieveTipController _controller;
        [SerializeField] private TipSettings _settings;
        [Tooltip("Child of the sieve placed at the rim nearest the basket, parenting the mesh, ash and embers.")]
        [SerializeField] private Transform _tipPivot;

        private Quaternion _restRotation;
        private float _startTime = -1f;
        private bool _refusing;

        private void Awake()
        {
            if (_controller == null || _settings == null || _tipPivot == null)
            {
                Debug.LogError($"{nameof(SieveTipMotion)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _restRotation = _tipPivot.localRotation;
        }

        private void OnEnable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.Tipped += OnTipped;
            _controller.TipRefused += OnTipRefused;
        }

        private void OnDisable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.Tipped -= OnTipped;
            _controller.TipRefused -= OnTipRefused;
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
                _tipPivot.localRotation = _restRotation;
                _startTime = -1f;
                return;
            }

            _tipPivot.localRotation = _restRotation * Quaternion.Euler(0f, 0f, Angle(progress));
        }

        private float Angle(float progress)
        {
            if (_refusing)
            {
                // Two quick side-to-side shakes that die out.
                return _settings.RefuseAngle * Mathf.Sin(progress * Mathf.PI * 4f) * (1f - progress);
            }

            // Out to the peak and back in one smooth arc.
            return _settings.TipAngle * Mathf.Sin(progress * Mathf.PI);
        }

        private void OnTipped(int moved)
        {
            Play(false);
        }

        private void OnTipRefused(TipOutcome outcome)
        {
            Play(true);
        }

        private void Play(bool refusing)
        {
            _refusing = refusing;
            _startTime = Time.time;
        }
    }
}
