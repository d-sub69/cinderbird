using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Owns the basket's <see cref="BasketContents"/> so other components can reach it through the Inspector.
    /// Also a pour target: while it stands on the ground its trigger is on, so the sieve can be poured into it.
    /// </summary>
    public sealed class Basket : MonoBehaviour, IEmberHolder, IPourTarget
    {
        public IEmberSource Source => Contents;

        public string DisplayName => "the basket";

        public IEmberSink Sink => Contents;

        // The basket view already animates new embers dropping in when the contents change.
        public void OnPoured(IReadOnlyList<Ember> embers, Vector3 fromPosition)
        {
        }

        [SerializeField] private BasketSettings _settings;

        private BasketContents _contents;

        /// <summary>
        /// Created on first access rather than in Awake: other objects' OnEnable can run before our Awake.
        /// </summary>
        public BasketContents Contents
        {
            get
            {
                if (_contents == null)
                {
                    int capacity = _settings != null ? _settings.Capacity : 1;
                    if (_settings == null)
                    {
                        Debug.LogError($"{nameof(Basket)} on '{name}' has no settings; capacity falls back to 1.", this);
                    }

                    _contents = new BasketContents(capacity);
                }

                return _contents;
            }
        }
    }
}
