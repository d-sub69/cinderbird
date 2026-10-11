using System.Collections.Generic;
using Cinderbird.Embers;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class EmberFieldTests
    {
        private static EmberField CreateField()
        {
            EmberField field = new EmberField();
            field.Add(new Ember(new Vector3(0f, 0f, 0f), 0));
            field.Add(new Ember(new Vector3(0.5f, 0f, 0f), 1));
            field.Add(new Ember(new Vector3(3f, 0f, 0f), 0));
            field.Add(new Ember(new Vector3(0f, 0.9f, 0f), 2));
            return field;
        }

        [Test]
        public void ExtractWithin_TakesOnlyEmbersInsideSphere()
        {
            EmberField field = CreateField();
            List<Ember> output = new List<Ember>();

            int extracted = field.ExtractWithin(Vector3.zero, 1f, output);

            Assert.That(extracted, Is.EqualTo(3));
            Assert.That(output.Count, Is.EqualTo(3));
            foreach (Ember ember in output)
            {
                Assert.That(ember.Position.magnitude, Is.LessThanOrEqualTo(1f));
            }
        }

        [Test]
        public void ExtractWithin_RemovesExtractedFromField()
        {
            EmberField field = CreateField();

            field.ExtractWithin(Vector3.zero, 1f, new List<Ember>());

            Assert.That(field.Count, Is.EqualTo(1));
            Assert.That(field[0].Position, Is.EqualTo(new Vector3(3f, 0f, 0f)));
        }

        [Test]
        public void ExtractWithin_SameSpotTwice_SecondFindsNothing()
        {
            EmberField field = CreateField();
            field.ExtractWithin(Vector3.zero, 1f, new List<Ember>());

            int second = field.ExtractWithin(Vector3.zero, 1f, new List<Ember>());

            Assert.That(second, Is.Zero);
        }

        [Test]
        public void ExtractWithin_AppendsToOutputWithoutClearing()
        {
            EmberField field = CreateField();
            List<Ember> output = new List<Ember> { new Ember(Vector3.one * 99f, 0) };

            field.ExtractWithin(new Vector3(3f, 0f, 0f), 0.1f, output);

            Assert.That(output.Count, Is.EqualTo(2));
        }

        [Test]
        public void ExtractWithin_KeepsTierIndex()
        {
            EmberField field = new EmberField();
            field.Add(new Ember(Vector3.zero, 2));
            List<Ember> output = new List<Ember>();

            field.ExtractWithin(Vector3.zero, 0.5f, output);

            Assert.That(output[0].TierIndex, Is.EqualTo(2));
        }
    }
}
