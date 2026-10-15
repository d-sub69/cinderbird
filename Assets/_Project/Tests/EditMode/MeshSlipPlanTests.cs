using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Sifting;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

namespace Cinderbird.Tests.EditMode
{
    public sealed class MeshSlipPlanTests
    {
        private const int Spark = 0;
        private const int Coal = 1;
        private static readonly Func<int, bool> OnlySparks = tier => tier == Spark;

        private static List<Ember> Load(int sparks, int coals)
        {
            List<Ember> load = new List<Ember>();
            for (int i = 0; i < sparks; i++)
            {
                load.Add(new Ember(new Vector3(i, 0f, 0f), Spark));
            }

            for (int i = 0; i < coals; i++)
            {
                load.Add(new Ember(new Vector3(i, 1f, 0f), Coal));
            }

            return load;
        }

        [Test]
        public void Plan_FullChance_EverySparkSlips_NoCoal()
        {
            MeshSlipPlan plan = new MeshSlipPlan();
            List<Ember> load = Load(5, 3);

            plan.Plan(load, 0, load.Count, OnlySparks, 1f, new Random(1));
            List<Ember> due = new List<Ember>();
            plan.TakeDue(0f, due);

            Assert.That(due.Count, Is.EqualTo(5));
            Assert.That(due.TrueForAll(e => e.TierIndex == Spark), Is.True);
            Assert.That(plan.Count, Is.Zero);
        }

        [Test]
        public void Plan_FineMesh_NothingSlips()
        {
            MeshSlipPlan plan = new MeshSlipPlan();
            List<Ember> load = Load(50, 0);

            plan.Plan(load, 0, load.Count, OnlySparks, 0f, new Random(1));

            Assert.That(plan.Count, Is.Zero);
        }

        [Test]
        public void Plan_OnlyTheNewLoad_NotEmbersAlreadyInTheSieve()
        {
            MeshSlipPlan plan = new MeshSlipPlan();
            List<Ember> sieve = Load(10, 0);

            plan.Plan(sieve, 6, 4, OnlySparks, 1f, new Random(1));

            Assert.That(plan.Count, Is.EqualTo(4));
        }

        [Test]
        public void Plan_ChanceIsAboutRightOverManyEmbers()
        {
            MeshSlipPlan plan = new MeshSlipPlan();
            List<Ember> load = Load(2000, 0);

            plan.Plan(load, 0, load.Count, OnlySparks, 0.3f, new Random(7));

            Assert.That(plan.Count, Is.InRange(520, 680));
        }

        [Test]
        public void TakeDue_SlipsHappenDuringTheShake_NotBeforeIt()
        {
            MeshSlipPlan plan = new MeshSlipPlan();
            List<Ember> load = Load(20, 0);
            plan.Plan(load, 0, load.Count, OnlySparks, 1f, new Random(3));
            List<Ember> due = new List<Ember>();

            plan.TakeDue(1f, due);
            Assert.That(due, Is.Empty, "Nothing falls before shaking starts");

            plan.TakeDue(0.5f, due);
            int halfway = due.Count;
            plan.TakeDue(0f, due);

            Assert.That(halfway, Is.InRange(1, 19), "Some fall during the shake, the rest later");
            Assert.That(due.Count, Is.EqualTo(20));
        }
    }
}
