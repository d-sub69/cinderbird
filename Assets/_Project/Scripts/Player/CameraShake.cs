using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// A small rotational shake of the camera: a steady hum (while something holds it) and short kicks that die down.
    /// Applied in LateUpdate on top of the rotation the controller sets every frame, so it never builds up or fights the
    /// player's look. Perlin noise, so it wobbles smoothly instead of jittering.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        [Tooltip("How fast the shake wobbles.")]
        [SerializeField, Min(0.1f)] private float _frequency = 18f;

        private float _hum;
        private float _kick;
        private float _kickDecay;

        /// <summary>A steady shake of this many degrees until set back to 0.</summary>
        public void SetHum(float degrees)
        {
            _hum = Mathf.Max(0f, degrees);
        }

        /// <summary>A shake of this many degrees that dies down over <paramref name="seconds"/>.</summary>
        public void Kick(float degrees, float seconds)
        {
            _kick = Mathf.Max(_kick, degrees);
            _kickDecay = _kick / Mathf.Max(0.01f, seconds);
        }

        private void LateUpdate()
        {
            // Paused: the look isn't set again this frame, so a shake would add up on the last one.
            if (Time.deltaTime <= 0f)
            {
                return;
            }

            _kick = Mathf.Max(0f, _kick - _kickDecay * Time.deltaTime);
            float amount = Mathf.Max(_hum, _kick);
            if (amount <= 0f)
            {
                return;
            }

            float time = Time.time * _frequency;
            Vector3 wobble = new Vector3(Mathf.PerlinNoise(time, 0f) * 2f - 1f, Mathf.PerlinNoise(0f, time) * 2f - 1f,
                (Mathf.PerlinNoise(time, time) * 2f - 1f) * 0.5f);
            transform.localRotation *= Quaternion.Euler(wobble * amount);
        }
    }
}
