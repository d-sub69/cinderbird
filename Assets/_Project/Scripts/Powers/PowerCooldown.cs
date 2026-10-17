using UnityEngine;

namespace Cinderbird.Powers
{
    /// <summary>When a power can be used again: ready at first, then once its cooldown has passed since the last use.</summary>
    public sealed class PowerCooldown
    {
        private float _readyAt = float.NegativeInfinity;

        public bool IsReady(float now)
        {
            return now >= _readyAt;
        }

        /// <summary>Seconds until it can be used again; 0 when ready.</summary>
        public float Remaining(float now)
        {
            return Mathf.Max(0f, _readyAt - now);
        }

        /// <summary>Ready again at once (e.g. a shorter cooldown was just bought).</summary>
        public void Reset()
        {
            _readyAt = float.NegativeInfinity;
        }

        /// <summary>Used now: ready again after <paramref name="seconds"/> (given at each use, so an upgrade counts at once).</summary>
        public void Use(float now, float seconds)
        {
            _readyAt = now + Mathf.Max(0f, seconds);
        }
    }
}
