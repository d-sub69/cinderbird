using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Powers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class WarmWindTests
    {
        private const float Cell = 0.25f;

        // An 8 × 8 m world with a flat slab of ash 1 m deep; below `layerSplit` it is layer 1.
        private static AshTerrain CreateSlab(float layerSplit = -1f)
        {
            VoxelWorld world = new VoxelWorld(16, 2, 1, 2, Cell, new Vector3(0f, -Cell, 0f));
            for (int z = 1; z < world.SamplesZ - 1; z++)
            {
                for (int y = 1; y < world.SamplesY - 1; y++)
                {
                    for (int x = 1; x < world.SamplesX - 1; x++)
                    {
                        float sampleY = world.SamplePosition(x, y, z).y;
                        world.SetDensity(x, y, z, Mathf.Clamp((1f - sampleY) / Cell, -1f, 1f));
                        world.SetLayer(x, y, z, (byte)(sampleY < layerSplit ? 1 : 0));
                    }
                }
            }

            return new AshTerrain(world, 2);
        }

        private static float TopAt(AshTerrain terrain, float x, float z)
        {
            terrain.World.TryFindTop(x, z, out float top);
            return top;
        }

        [Test]
        public void Blow_TakesTheTopOffTheArea_AndFadesAtTheRim()
        {
            AshTerrain terrain = CreateSlab();

            DigResult result = terrain.BlowSurface(new Vector3(4f, 1f, 4f), 2f, 0.3f, new List<LooseEmber>(), new List<BuriedFind>());

            Assert.That(TopAt(terrain, 4f, 4f), Is.EqualTo(0.7f).Within(0.05f), "the full depth in the middle");
            Assert.That(TopAt(terrain, 7f, 4f), Is.EqualTo(1f).Within(0.05f), "nothing outside the area");
            Assert.That(TopAt(terrain, 5.8f, 4f), Is.GreaterThan(0.72f), "shallower toward the rim");
            float fullDisc = Mathf.PI * 2f * 2f * 0.3f;
            Assert.That(result.RemovedVolume, Is.InRange(fullDisc * 0.6f, fullDisc * 1.1f));
        }

        [Test]
        public void Blow_LaysTheEmbersItUncoversOnTheSurface_AndLeavesDeeperOnesBuried()
        {
            AshTerrain terrain = CreateSlab();
            terrain.PlaceEmber(new Ember(new Vector3(4.2f, 0.85f, 4.1f), 0, 0));
            terrain.PlaceEmber(new Ember(new Vector3(3.8f, 0.3f, 4f), 0, 0));
            terrain.PlaceEmber(new Ember(new Vector3(7f, 0.9f, 7f), 0, 0));
            List<LooseEmber> exposed = new List<LooseEmber>();

            terrain.BlowSurface(new Vector3(4f, 1f, 4f), 2f, 0.3f, exposed, new List<BuriedFind>());

            Assert.That(exposed.Count, Is.EqualTo(1), "only the one in the blown-off skin");
            Assert.That(exposed[0].Ember.Position.y, Is.EqualTo(TopAt(terrain, 4.2f, 4.1f)).Within(0.01f), "lying on the new surface");
            Assert.That(terrain.BuriedEmberCount, Is.EqualTo(3), "still on the mountain until collected");
        }

        [Test]
        public void Blow_LeavesSealedLayersAlone()
        {
            AshTerrain terrain = CreateSlab(0.9f);

            DigResult result = terrain.BlowSurface(new Vector3(4f, 1f, 4f), 2f, 0.3f, new List<LooseEmber>(), new List<BuriedFind>());

            Assert.That(TopAt(terrain, 4f, 4f), Is.GreaterThan(0.84f), "stops on the sealed layer");
            Assert.That(result.RemovedVolume, Is.GreaterThan(0f), "the open skin above it still goes");
        }

        [Test]
        public void Blow_UncoversOfferingsInTheSkin()
        {
            AshTerrain terrain = CreateSlab();
            terrain.PlaceFind(7, new Vector3(4f, 0.85f, 4f));
            List<BuriedFind> finds = new List<BuriedFind>();

            terrain.BlowSurface(new Vector3(4f, 1f, 4f), 2f, 0.3f, new List<LooseEmber>(), finds);

            Assert.That(finds.Count, Is.EqualTo(1));
            Assert.That(finds[0].Id, Is.EqualTo(7));
        }

        [Test]
        public void Cooldown_ReadyFirst_ThenAfterItsTime()
        {
            PowerCooldown cooldown = new PowerCooldown();
            Assert.That(cooldown.IsReady(0f), Is.True);

            cooldown.Use(10f, 60f);

            Assert.That(cooldown.IsReady(30f), Is.False);
            Assert.That(cooldown.Remaining(30f), Is.EqualTo(40f));
            Assert.That(cooldown.IsReady(70f), Is.True);

            cooldown.Use(80f, 60f);
            cooldown.Reset();

            Assert.That(cooldown.IsReady(81f), Is.True, "reset: ready at once");
        }
    }
}
