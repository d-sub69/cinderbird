using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Which glimmers to show and where each one is drawn: the candidates nearest the viewer, each matched to a
    /// same-tier glimmer shown close by before (taken as that ember having been carried by sliding ash), so it
    /// glides from there instead of jumping. Plain C# with fixed buffers, so picking allocates nothing.
    /// </summary>
    public sealed class GlimmerSelection
    {
        private readonly GlimmerPoint[] _picked;
        private readonly float[] _pickedDistances;
        // Per slot: where it is drawn now, where it is heading, and its tier; plus the previous pick's, for matching.
        private readonly Vector3[] _shown;
        private readonly Vector3[] _targets;
        private readonly int[] _tiers;
        private readonly Vector3[] _previousShown;
        private readonly Vector3[] _previousTargets;
        private readonly int[] _previousTiers;
        private readonly bool[] _previousUsed;

        public GlimmerSelection(int capacity)
        {
            _picked = new GlimmerPoint[capacity];
            _pickedDistances = new float[capacity];
            _shown = new Vector3[capacity];
            _targets = new Vector3[capacity];
            _tiers = new int[capacity];
            _previousShown = new Vector3[capacity];
            _previousTargets = new Vector3[capacity];
            _previousTiers = new int[capacity];
            _previousUsed = new bool[capacity];
        }

        public int Capacity => _picked.Length;

        /// <summary>How many glimmers the last <see cref="Pick"/> chose (slots 0 .. Count-1).</summary>
        public int Count { get; private set; }

        public GlimmerPoint this[int slot] => _picked[slot];

        /// <summary>Where the glimmer in a slot is drawn right now (it may still be gliding).</summary>
        public Vector3 ShownAt(int slot)
        {
            return _shown[slot];
        }

        /// <summary>
        /// Picks the candidates nearest <paramref name="eye"/> (all in the same space), nearest first, and matches
        /// them to the previous pick.
        /// </summary>
        /// <param name="followDistance">A previous same-tier glimmer within this many meters counts as the same ember.</param>
        public void Pick(IReadOnlyList<List<GlimmerPoint>> candidatesPerChunk, Vector3 eye, float followDistance)
        {
            int count = PickNearest(candidatesPerChunk, eye);
            MatchToPrevious(count, followDistance);
            Count = count;
        }

        /// <summary>Moves every gliding glimmer a step toward its ember's surface point.</summary>
        /// <param name="follow">Share of the remaining way to cover this step (0..1).</param>
        /// <returns>Whether the slot's drawn position changed.</returns>
        public bool Glide(int slot, float follow)
        {
            if (_shown[slot] == _targets[slot])
            {
                return false;
            }

            _shown[slot] = Vector3.Lerp(_shown[slot], _targets[slot], follow);
            return true;
        }

        // A small insertion sort keeps the N nearest; N is a couple dozen.
        private int PickNearest(IReadOnlyList<List<GlimmerPoint>> candidatesPerChunk, Vector3 eye)
        {
            int capacity = _picked.Length;
            int count = 0;
            for (int chunk = 0; chunk < candidatesPerChunk.Count; chunk++)
            {
                List<GlimmerPoint> candidates = candidatesPerChunk[chunk];
                for (int i = 0; i < candidates.Count; i++)
                {
                    float distance = (candidates[i].SurfacePosition - eye).sqrMagnitude;
                    if (count == capacity && distance >= _pickedDistances[count - 1])
                    {
                        continue;
                    }

                    int slot = count < capacity ? count++ : count - 1;
                    while (slot > 0 && _pickedDistances[slot - 1] > distance)
                    {
                        _pickedDistances[slot] = _pickedDistances[slot - 1];
                        _picked[slot] = _picked[slot - 1];
                        slot--;
                    }

                    _pickedDistances[slot] = distance;
                    _picked[slot] = candidates[i];
                }
            }

            return count;
        }

        private void MatchToPrevious(int count, float followDistance)
        {
            int previous = Count;
            for (int j = 0; j < previous; j++)
            {
                _previousShown[j] = _shown[j];
                _previousTargets[j] = _targets[j];
                _previousTiers[j] = _tiers[j];
                _previousUsed[j] = false;
            }

            float reach = followDistance * followDistance;
            for (int i = 0; i < count; i++)
            {
                GlimmerPoint glimmer = _picked[i];
                int match = -1;
                float closest = reach;
                for (int j = 0; j < previous; j++)
                {
                    if (_previousUsed[j] || _previousTiers[j] != glimmer.TierIndex)
                    {
                        continue;
                    }

                    float distance = (_previousTargets[j] - glimmer.SurfacePosition).sqrMagnitude;
                    if (distance <= closest)
                    {
                        closest = distance;
                        match = j;
                    }
                }

                if (match >= 0)
                {
                    _previousUsed[match] = true;
                }

                _shown[i] = match >= 0 ? _previousShown[match] : glimmer.SurfacePosition;
                _targets[i] = glimmer.SurfacePosition;
                _tiers[i] = glimmer.TierIndex;
            }
        }
    }
}
