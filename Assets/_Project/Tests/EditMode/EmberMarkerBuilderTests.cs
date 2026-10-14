using Cinderbird.Embers;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class EmberMarkerBuilderTests
    {
        [Test]
        public void Add_MakesOneClosedMarkerPerEmber_AroundItsCenter()
        {
            EmberMarkerBuilder builder = new EmberMarkerBuilder();

            builder.Add(new Vector3(1f, 2f, 3f), 0.1f, Color.red);
            builder.Add(Vector3.zero, 0.1f, Color.blue);

            Assert.That(builder.Vertices.Count, Is.EqualTo(12));
            Assert.That(builder.Colors.Count, Is.EqualTo(12));
            Assert.That(builder.Indices.Count, Is.EqualTo(48), "8 triangles each");
            Assert.That(builder.Indices[24], Is.GreaterThanOrEqualTo(6), "the second marker uses its own vertices");
            for (int i = 0; i < 6; i++)
            {
                Assert.That(Vector3.Distance(builder.Vertices[i], new Vector3(1f, 2f, 3f)), Is.EqualTo(0.1f).Within(1e-5f));
                Assert.That(builder.Colors[i], Is.EqualTo(Color.red));
            }
        }

        [Test]
        public void Clear_StartsOver()
        {
            EmberMarkerBuilder builder = new EmberMarkerBuilder();
            builder.Add(Vector3.zero, 0.1f, Color.red);

            builder.Clear();

            Assert.That(builder.Vertices, Is.Empty);
            Assert.That(builder.Indices, Is.Empty);
        }
    }
}
