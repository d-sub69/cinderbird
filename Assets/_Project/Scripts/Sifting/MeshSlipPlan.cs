using System;
using System.Collections.Generic;
using Cinderbird.Embers;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Which embers of a freshly scooped load slip through the sieve's mesh, and when: each small enough ember slips
    /// with the mesh's chance, at a random point while the load is shaken out. Rolled once per load, so shaking
    /// harder or softer doesn't change how many are lost, only when they fall.
    /// </summary>
    public sealed class MeshSlipPlan
    {
        // They fall somewhere in the middle of the shake, never right at its start or end.
        private const float EarliestFraction = 0.9f;
        private const float LatestFraction = 0.1f;

        private readonly List<Ember> _embers = new List<Ember>();
        private readonly List<float> _atAshFraction = new List<float>();

        /// <summary>Embers still waiting to slip.</summary>
        public int Count => _embers.Count;

        /// <param name="load">The sieve's embers; the load is <paramref name="count"/> of them from <paramref name="first"/>.</param>
        /// <param name="canSlip">Whether embers of a tier are small enough to slip (by tier index).</param>
        /// <param name="chance">The mesh's slip chance per ember (0..1).</param>
        public void Plan(IReadOnlyList<Ember> load, int first, int count, Func<int, bool> canSlip, float chance, Random random)
        {
            Clear();
            if (chance <= 0f)
            {
                return;
            }

            int end = Math.Min(load.Count, first + count);
            for (int i = Math.Max(0, first); i < end; i++)
            {
                Ember ember = load[i];
                if (!canSlip(ember.TierIndex) || random.NextDouble() >= chance)
                {
                    continue;
                }

                _embers.Add(ember);
                _atAshFraction.Add(LatestFraction + (float)random.NextDouble() * (EarliestFraction - LatestFraction));
            }
        }

        public void Clear()
        {
            _embers.Clear();
            _atAshFraction.Clear();
        }

        /// <summary>Moves the embers due once the load is shaken down to <paramref name="ashFraction"/> into the output.</summary>
        /// <param name="ashFraction">Share of the load's ash still in the sieve: 1 just scooped, 0 shaken out.</param>
        public void TakeDue(float ashFraction, List<Ember> output)
        {
            for (int i = _embers.Count - 1; i >= 0; i--)
            {
                if (ashFraction > _atAshFraction[i])
                {
                    continue;
                }

                output.Add(_embers[i]);
                _embers.RemoveAt(i);
                _atAshFraction.RemoveAt(i);
            }
        }
    }
}
