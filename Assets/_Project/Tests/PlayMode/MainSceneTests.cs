using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cinderbird.Core;
using Cinderbird.Embers;
using Cinderbird.Save;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cinderbird.Tests.PlayMode
{
    /// <summary>
    /// Smoke tests on the real game scene: it starts up, and the dig → drain → hole chain works end to end. Saves
    /// go to a temporary folder, so these never read or overwrite the real save.
    /// </summary>
    public sealed class MainSceneTests
    {
        private const string SceneName = "Main";
        private const float StartupTimeout = 30f;

        private string _saveFolder;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _saveFolder = Path.Combine(Path.GetTempPath(), "CinderbirdPlayModeTests", Path.GetRandomFileName());
            SaveController.RedirectSavesForTests(_saveFolder);
            SceneManager.LoadScene(SceneName);
            // The scene loads on the next frame.
            yield return null;

            GameBootstrap bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null, "Main has a GameBootstrap");
            float waited = 0f;
            while (bootstrap.StartupSeconds <= 0f && waited < StartupTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(bootstrap.StartupSeconds, Is.GreaterThan(0f), "start-up finished in time");
        }

        [TearDown]
        public void CleanUp()
        {
            SaveController.RedirectSavesForTests(null);
            if (Directory.Exists(_saveFolder))
            {
                Directory.Delete(_saveFolder, true);
            }
        }

        [Test]
        public void StartUp_BuildsTheMountainWithEmbers()
        {
            VoxelTerrain pile = Object.FindAnyObjectByType<VoxelTerrain>();

            Assert.That(pile.IsBuilt, Is.True);
            Assert.That(pile.TotalAshVolume, Is.GreaterThan(1f));
            Assert.That(pile.BuriedEmberCount, Is.GreaterThan(0));
            Assert.That(pile.DeepestUnlockedLayer, Is.Zero, "a fresh game starts with only the outer layer open");
        }

        [UnityTest]
        public IEnumerator Scoop_DrainsOpenAHoleAndCountsTheAsh()
        {
            VoxelTerrain pile = Object.FindAnyObjectByType<VoxelTerrain>();
            Assert.That(TryFindTop(pile, out Vector3 top), Is.True, "found the pile's surface from above");
            Vector3 center = top + Vector3.down * 0.2f;
            float volumeBefore = pile.TotalAshVolume;
            bool dug = false;
            pile.Dug += (result, localCenter, radius) => dug = true;

            DigResult result = pile.Dig(center, 0.4f, new List<Ember>(), null, 0.25f, 0.5f);

            Assert.That(result.RemovedVolume, Is.GreaterThan(0f));
            Assert.That(pile.TotalAshVolume, Is.EqualTo(volumeBefore - result.RemovedVolume).Within(1e-3f));
            float waited = 0f;
            while (!dug && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.That(dug, Is.True, "the hole finished draining");
            Assert.That(pile.SampleDensityAt(center), Is.LessThan(0f), "the scooped spot is open");
        }

        [UnityTest]
        public IEnumerator Scoop_WhileTheLastStillDrains_FinishesTheFirstAtOnce()
        {
            VoxelTerrain pile = Object.FindAnyObjectByType<VoxelTerrain>();
            Assert.That(TryFindTop(pile, out Vector3 top), Is.True);
            int dugCount = 0;
            pile.Dug += (result, localCenter, radius) => dugCount++;

            pile.Dig(top + Vector3.down * 0.2f, 0.4f, new List<Ember>(), null, 0.25f, 2f);
            yield return null;
            pile.Dig(top + new Vector3(0.6f, -0.2f, 0f), 0.4f, new List<Ember>(), null, 0.25f, 2f);

            Assert.That(dugCount, Is.EqualTo(1), "the first scoop's hole was completed before the second dig");
        }

        // The highest ash under the pile's middle, found by a ray from above.
        private static bool TryFindTop(VoxelTerrain pile, out Vector3 top)
        {
            Vector3 above = pile.transform.position + Vector3.up * 100f;
            RaycastHit[] hits = Physics.RaycastAll(above, Vector3.down, 200f);
            top = default;
            float best = float.MinValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<VoxelTerrain>() == pile && hit.point.y > best)
                {
                    best = hit.point.y;
                    top = hit.point;
                }
            }

            return best > float.MinValue;
        }
    }
}
