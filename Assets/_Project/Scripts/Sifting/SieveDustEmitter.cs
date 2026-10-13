using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Emits dust particles in proportion to the ash shaken out, so heavier shaking pours more dust.
    /// The dust takes the color of the layers the current load was dug from.
    /// </summary>
    public sealed class SieveDustEmitter : MonoBehaviour
    {
        [SerializeField] private SieveShakeController _controller;
        [SerializeField] private ShakeSettings _settings;
        [Tooltip("Particle system with its own Emission rate at 0; this script emits manually.")]
        [SerializeField] private ParticleSystem _dust;
        [Tooltip("Optional: colors the dust by the load's layers. Without it the particle system's own color is used.")]
        [SerializeField] private Sieve _sieve;

        // Fractional particles carried to the next frame, so small per-frame amounts still add up.
        private float _pendingParticles;

        private void Awake()
        {
            if (_controller == null || _settings == null || _dust == null)
            {
                Debug.LogError($"{nameof(SieveDustEmitter)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.AshShakenOut += OnAshShakenOut;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.AshShakenOut -= OnAshShakenOut;
            }
        }

        private void OnAshShakenOut(float volume)
        {
            _pendingParticles += volume * _settings.DustParticlesPerCubicMeter;
            int count = (int)_pendingParticles;
            if (count <= 0)
            {
                return;
            }

            _pendingParticles -= count;
            if (_sieve == null)
            {
                _dust.Emit(count);
                return;
            }

            // Only the color is overridden; everything else still comes from the particle system.
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams { startColor = _sieve.Contents.DustColor };
            _dust.Emit(emit, count);
        }
    }
}
