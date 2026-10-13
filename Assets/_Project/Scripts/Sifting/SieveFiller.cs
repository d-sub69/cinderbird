using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Thin adapter around <see cref="ScoopTransfer"/>: runs the pour of a scooped load into the sieve and
    /// hands the load over when it has arrived. Views use the progress to draw the ash rising.
    /// </summary>
    public sealed class SieveFiller : MonoBehaviour
    {
        [SerializeField] private Sieve _sieve;
        [SerializeField] private SieveSettings _settings;

        private readonly ScoopTransfer _transfer = new ScoopTransfer();

        /// <summary>Fired when a pour starts, with the ash volume and where in the world it was dug.</summary>
        public event Action<float, Vector3> FillStarted;

        /// <summary>Fired whenever the incoming load's progress changes (every frame while pouring) and on hand-over.</summary>
        public event Action IncomingChanged;

        public bool IsFilling => _transfer.IsActive;
        public float IncomingVolume => _transfer.Volume;
        public IReadOnlyList<Ember> IncomingEmbers => _transfer.Embers;

        /// <summary>Seconds a scooped load takes to pour in; the hole in the pile opens over the same time.</summary>
        public float PourDuration => _settings != null ? _settings.FillDuration : 0f;
        public float IncomingProgress => _transfer.Progress;
        public Color IncomingAshColor => _transfer.AshColor;
        public Color IncomingDustColor => _transfer.DustColor;

        private void Awake()
        {
            if (_sieve == null || _settings == null)
            {
                Debug.LogError($"{nameof(SieveFiller)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        /// <param name="digPoint">World position the ash came from, where the pour starts.</param>
        /// <returns>False if a pour is already running.</returns>
        public bool TryBegin(float volume, List<Ember> embers, Vector3 digPoint)
        {
            return TryBegin(volume, embers, digPoint, new Color(0.55f, 0.55f, 0.55f, 1f), new Color(0.78f, 0.76f, 0.74f, 0.8f));
        }

        /// <param name="ashColor">Color of the load's ash (from the layers it was dug from).</param>
        /// <param name="dustColor">Color of the dust it sheds.</param>
        public bool TryBegin(float volume, List<Ember> embers, Vector3 digPoint, Color ashColor, Color dustColor)
        {
            if (!_transfer.TryBegin(volume, embers, _settings.FillDuration, ashColor, dustColor))
            {
                return false;
            }

            FillStarted?.Invoke(volume, digPoint);
            IncomingChanged?.Invoke();
            return true;
        }

        private void Update()
        {
            if (!_transfer.IsActive)
            {
                return;
            }

            if (_transfer.Tick(Time.deltaTime))
            {
                HandOver();
            }
            else
            {
                IncomingChanged?.Invoke();
            }
        }

        // Switching tools mid-pour: land the load at once so nothing is ever stuck half-poured.
        private void OnDisable()
        {
            if (_transfer.IsActive)
            {
                HandOver();
            }
        }

        private void HandOver()
        {
            _transfer.HandOverTo(_sieve.Contents);
            IncomingChanged?.Invoke();
        }
    }
}
