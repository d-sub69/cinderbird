using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Wobbles the sieve around its resting pose in proportion to shake energy.
    /// Perlin noise instead of random values gives a smooth, organic jiggle rather than jitter.
    /// </summary>
    public sealed class SieveShakeMotion : MonoBehaviour
    {
        // Offsets into Perlin space so each axis follows its own curve.
        private const float AxisOffsetX = 0f;
        private const float AxisOffsetY = 31.7f;
        private const float AxisOffsetZ = 67.3f;

        [SerializeField] private SieveShakeController _controller;
        [SerializeField] private ShakeSettings _settings;
        [Tooltip("What to jiggle; usually the Sieve object itself.")]
        [SerializeField] private Transform _target;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private bool _atRest = true;

        private void Awake()
        {
            if (_controller == null || _settings == null || _target == null)
            {
                Debug.LogError($"{nameof(SieveShakeMotion)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _restPosition = _target.localPosition;
            _restRotation = _target.localRotation;
        }

        // LateUpdate so the jiggle applies after the shake controller has updated energy this frame.
        private void LateUpdate()
        {
            float energy = _controller.Energy;
            if (energy <= 0f)
            {
                ReturnToRest();
                return;
            }

            float time = Time.time * _settings.JiggleFrequency;
            Vector3 noise = new Vector3(
                Wave(time, AxisOffsetX),
                Wave(time, AxisOffsetY),
                Wave(time, AxisOffsetZ));

            _target.localPosition = _restPosition + noise * (_settings.JigglePosition * energy);
            _target.localRotation = _restRotation * Quaternion.Euler(noise * (_settings.JiggleRotation * energy));
            _atRest = false;
        }

        private void ReturnToRest()
        {
            if (_atRest)
            {
                return;
            }

            _target.localPosition = _restPosition;
            _target.localRotation = _restRotation;
            _atRest = true;
        }

        // Perlin output is roughly 0..1; remap to -1..1 so the wobble is centered on the rest pose.
        private static float Wave(float time, float offset)
        {
            return Mathf.PerlinNoise(time, offset) * 2f - 1f;
        }
    }
}
