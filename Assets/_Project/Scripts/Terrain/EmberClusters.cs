using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// The clusters as they stand now, from the embers still buried in them wherever they are: a cluster dug into holds
    /// fewer embers, one carried down by sliding ash moves with them. Kept as per-chunk partial sums, and only chunks whose
    /// ash or embers changed are counted again, so a refresh after a dig costs a chunk or two.
    /// </summary>
    public sealed class EmberClusters
    {
        // A uniform sphere's embers lie on average this share of its radius from the middle (RMS √(3/5)).
        private const float RmsToRadius = 1.29f;
        private const float MinRadius = 0.2f;

        private struct Partial
        {
            public ushort Id;
            public int Count;
            public float Weight;
            public Vector3 Sum;
            public float SumSquares;
            public int BestTier;
        }

        private readonly byte[] _layerOf;
        private readonly float[] _weightByTier;
        private readonly List<Partial>[] _partials;
        private readonly int[] _seenVersions;
        private readonly ClusterState[] _states;
        private readonly Partial[] _totals;
        private readonly List<int> _changedChunks = new List<int>();

        /// <param name="layout">The clusters as buried (ids 1 and up).</param>
        /// <param name="chunkCount">Chunks of the terrain.</param>
        /// <param name="weightByTier">What each tier adds to a cluster's weight (its glow).</param>
        public EmberClusters(IReadOnlyList<EmberCluster> layout, int chunkCount, IReadOnlyList<float> weightByTier)
        {
            int highest = 0;
            foreach (EmberCluster cluster in layout)
            {
                highest = Math.Max(highest, cluster.Id);
            }

            _layerOf = new byte[highest + 1];
            foreach (EmberCluster cluster in layout)
            {
                _layerOf[cluster.Id] = cluster.Layer;
            }

            _weightByTier = new float[weightByTier.Count];
            for (int i = 0; i < _weightByTier.Length; i++)
            {
                _weightByTier[i] = weightByTier[i];
            }

            _partials = new List<Partial>[chunkCount];
            _seenVersions = new int[chunkCount];
            for (int chunk = 0; chunk < chunkCount; chunk++)
            {
                _partials[chunk] = new List<Partial>();
                _seenVersions[chunk] = -1;
            }

            _states = new ClusterState[highest + 1];
            _totals = new Partial[highest + 1];
        }

        /// <summary>Highest cluster id + 1: the states run from 1 to this − 1 (0 is "no cluster").</summary>
        public int Capacity => _states.Length;

        /// <summary>Goes up whenever any cluster changed, so viewers can tell they need to look again.</summary>
        public int Version { get; private set; }

        /// <summary>Chunks found changed by the last <see cref="Refresh"/> (valid until the next).</summary>
        public IReadOnlyList<int> ChangedChunks => _changedChunks;

        public ClusterState this[int id] => id > 0 && id < _states.Length ? _states[id] : default;

        /// <summary>Counts chunks that changed since the last refresh again.</summary>
        /// <returns>True when any cluster changed.</returns>
        public bool Refresh(AshTerrain terrain)
        {
            _changedChunks.Clear();
            for (int chunk = 0; chunk < _partials.Length; chunk++)
            {
                int version = terrain.ChangeVersionOf(chunk);
                if (version == _seenVersions[chunk])
                {
                    continue;
                }

                _seenVersions[chunk] = version;
                _changedChunks.Add(chunk);
                Count(terrain.EmbersIn(chunk), _partials[chunk]);
            }

            if (_changedChunks.Count == 0)
            {
                return false;
            }

            Total();
            Version++;
            return true;
        }

        /// <summary>The nearest cluster of a layer that still holds embers.</summary>
        public bool TryNearest(Vector3 from, int layer, out ClusterState nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            for (int id = 1; id < _states.Length; id++)
            {
                ClusterState state = _states[id];
                if (state.IsEmpty || state.Layer != layer)
                {
                    continue;
                }

                float distance = (state.Center - from).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = state;
                }
            }

            return best < float.MaxValue;
        }

        private void Count(EmberField embers, List<Partial> partials)
        {
            partials.Clear();
            for (int i = 0; i < embers.Count; i++)
            {
                Ember ember = embers[i];
                if (ember.Cluster == 0 || ember.Cluster >= _states.Length)
                {
                    continue;
                }

                int slot = IndexOf(partials, ember.Cluster);
                Partial partial = slot >= 0 ? partials[slot] : new Partial { Id = ember.Cluster, BestTier = -1 };
                partial.Count++;
                partial.Weight += ember.TierIndex >= 0 && ember.TierIndex < _weightByTier.Length ? _weightByTier[ember.TierIndex] : 0f;
                partial.Sum += ember.Position;
                partial.SumSquares += ember.Position.sqrMagnitude;
                partial.BestTier = Math.Max(partial.BestTier, ember.TierIndex);
                if (slot >= 0)
                {
                    partials[slot] = partial;
                }
                else
                {
                    partials.Add(partial);
                }
            }
        }

        // A chunk holds parts of a few clusters at most, so a short linear search beats a dictionary here.
        private static int IndexOf(List<Partial> partials, ushort id)
        {
            for (int i = 0; i < partials.Count; i++)
            {
                if (partials[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Total()
        {
            Array.Clear(_totals, 0, _totals.Length);
            foreach (List<Partial> partials in _partials)
            {
                foreach (Partial partial in partials)
                {
                    ref Partial total = ref _totals[partial.Id];
                    if (total.Count == 0)
                    {
                        total.BestTier = -1;
                    }

                    total.Count += partial.Count;
                    total.Weight += partial.Weight;
                    total.Sum += partial.Sum;
                    total.SumSquares += partial.SumSquares;
                    total.BestTier = Math.Max(total.BestTier, partial.BestTier);
                }
            }

            for (int id = 1; id < _states.Length; id++)
            {
                Partial total = _totals[id];
                if (total.Count == 0)
                {
                    _states[id] = new ClusterState((ushort)id, _layerOf[id], 0, 0f, Vector3.zero, 0f, -1);
                    continue;
                }

                Vector3 center = total.Sum / total.Count;
                float spread = Mathf.Sqrt(Mathf.Max(0f, total.SumSquares / total.Count - center.sqrMagnitude));
                _states[id] = new ClusterState((ushort)id, _layerOf[id], total.Count, total.Weight, center,
                    Mathf.Max(MinRadius, spread * RmsToRadius), total.BestTier);
            }
        }
    }
}
