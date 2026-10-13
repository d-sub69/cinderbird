using System.Collections;
using Cinderbird.Save;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Core
{
    /// <summary>
    /// The game's single, explicit start-up sequence, so nothing depends on which Awake or Start Unity happens
    /// to run first: show the loading screen → build the mountain (the nest and glimmers react to it being
    /// built) → load the save on top → hand control to the player.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private VoxelTerrain _pile;
        [SerializeField] private SaveController _save;
        [SerializeField] private PauseController _pause;
        [Tooltip("Full-screen cover shown while the mountain is built; hidden when play starts.")]
        [SerializeField] private GameObject _loadingScreen;

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

            _pile.Build();
            _save.LoadAtStart();

            // One more frame so the first frame shown already has the restored terrain uploaded.
            yield return null;
            _loadingScreen.SetActive(false);
            _pause.FinishLoading();
        }
    }
}
