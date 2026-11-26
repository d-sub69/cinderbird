using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>
    /// When a deeper layer opens, the nest's heat melts away what is left of the layers above it (the terrain does the
    /// melting): the embers that were still in that ash fly to the nest and pay out, and sparks rise from the melting
    /// crust. The player never has to dig barren ash to reach the next layer.
    /// </summary>
    public sealed class CrustMelt : MonoBehaviour
    {
        // Sparks rising from the crust: enough to read as the whole mountain giving up its heat, not one per ember.
        private const int MaxSparks = 400;

        [SerializeField] private VoxelTerrain _pile;
        [SerializeField] private NestController _nest;
        [Tooltip("Optional: sparks rising from where the melted embers lay.")]
        [SerializeField] private ParticleSystem _sparks;

        private readonly List<Ember> _released = new List<Ember>();
        private readonly List<Ember> _paying = new List<Ember>();

        /// <summary>A short message for the player (what the melt gave the nest).</summary>
        public event Action<string> Notice;

        private void Awake()
        {
            if (_pile == null || _nest == null)
            {
                Debug.LogError($"{nameof(CrustMelt)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_pile != null)
            {
                _pile.CrustMelted += OnCrustMelted;
            }
        }

        private void OnDisable()
        {
            if (_pile != null)
            {
                _pile.CrustMelted -= OnCrustMelted;
            }
        }

        private void OnCrustMelted(int layer, IReadOnlyList<Ember> embers)
        {
            EmitSparks(embers);
            Notice?.Invoke(embers.Count > 0
                ? $"The crust melts away: {embers.Count} embers fly to the nest"
                : "The crust melts away");
            for (int i = 0; i < embers.Count; i++)
            {
                _released.Add(embers[i]);
            }
        }

        // Paid a frame later, not inside the unlock: the Heat it brings may open the next layer, which shouldn't happen
        // in the middle of opening this one.
        private void Update()
        {
            if (_released.Count == 0)
            {
                return;
            }

            Vector3 from = _pile.transform.TransformPoint(Centroid(_released));
            _paying.Clear();
            _paying.AddRange(_released);
            _released.Clear();
            // Into the nest as a pour would, so it pays, flares and shows the flights.
            _nest.Sink.Add(_paying);
            _nest.OnPoured(_paying, from);
        }

        private void EmitSparks(IReadOnlyList<Ember> embers)
        {
            if (_sparks == null || embers.Count == 0)
            {
                return;
            }

            ParticleSystem.EmitParams spark = new ParticleSystem.EmitParams();
            int step = Mathf.Max(1, embers.Count / MaxSparks);
            for (int i = 0; i < embers.Count; i += step)
            {
                spark.position = _pile.transform.TransformPoint(embers[i].Position);
                _sparks.Emit(spark, 1);
            }
        }

        private static Vector3 Centroid(IReadOnlyList<Ember> embers)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < embers.Count; i++)
            {
                sum += embers[i].Position;
            }

            return sum / Mathf.Max(1, embers.Count);
        }
    }
}
