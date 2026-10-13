using System.Collections;
using System.Diagnostics;
using Cinderbird.Save;
using Cinderbird.Terrain;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Cinderbird.Core
{
    /// <summary>
    /// The game's single, explicit start-up sequence, so nothing depends on which Awake or Start Unity happens
    /// to run first: show the loading screen → build the mountain (the nest and glimmers react to it being
    /// built) → load the save on top → hand control to the player. Times each part for the performance budget.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private VoxelTerrain _pile;
        [SerializeField] private SaveController _save;
        [SerializeField] private PauseController _pause;
        [Tooltip("Full-screen cover shown while the mountain is built; hidden when play starts.")]
        [SerializeField] private GameObject _loadingScreen;

        private readonly Stopwatch _startup = new Stopwatch();

        /// <summary>From this scene's Awake to the player having control; 0 until then.</summary>
        public float StartupSeconds { get; private set; }

        public float BuildSeconds { get; private set; }

        public float LoadSeconds { get; private set; }

        private void Awake()
        {
            _startup.Start();
        }

        private IEnumerator Start()
        {
            if (_pile == null || _save == null || _pause == null || _loadingScreen == null)
            {
                Debug.LogError($"{nameof(GameBootstrap)} on '{name}' has unassigned references.", this);
                yield break;
            }

            _loadingScreen.SetActive(true);
            // Let the loading screen render before the long, blocking build.
            yield return null;

            Stopwatch step = Stopwatch.StartNew();
            _pile.Build();
            BuildSeconds = (float)step.Elapsed.TotalSeconds;

            step.Restart();
            _save.LoadAtStart();
            LoadSeconds = (float)step.Elapsed.TotalSeconds;

            // One more frame so the first frame shown already has the restored terrain uploaded.
            yield return null;
            _loadingScreen.SetActive(false);
            _pause.FinishLoading();

            StartupSeconds = (float)_startup.Elapsed.TotalSeconds;
            Debug.Log($"Start-up took {StartupSeconds:F2} s (mountain {BuildSeconds:F2} s, save {LoadSeconds:F2} s).");
        }
    }
}
