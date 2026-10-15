using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cinderbird.Core;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Nest;
using Cinderbird.Offerings;
using Cinderbird.Player;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Cinderbird.Save
{
    /// <summary>
    /// Thin adapter between the game and its <see cref="SaveSlot"/>: loads the save when the bootstrap says so, autosaves on a
    /// timer, after feeding the nest and on quit, and handles the debug keys where <see cref="DevTools"/> are on (F5
    /// save, F9 reload the save, F8 delete it and start fresh). Saving copies the game state on the main thread (quick) and packs and
    /// writes it on a background thread, so autosaves don't stall a frame.
    /// </summary>
    public sealed class SaveController : MonoBehaviour
    {
        private static readonly ProfilerMarker CaptureMarker = new ProfilerMarker("Cinderbird.Save");

        // Longest an autosave waits for sliding ash to come to rest.
        private const float MaxWaitForSettling = 30f;
        // Ahead of a save, at most this many chunks that stopped changing are copied per frame...
        private const int WarmChunksPerFrame = 1;
        // ...once they have stayed the same for this many frames.
        private const int WarmAfterUnchangedFrames = 30;

        // F9 reloads the scene to load the save; this makes that one start load it even when the Editor
        // setting says to start fresh. Static because the reload replaces this object.
        private static bool _loadOnNextStart;

        // Editor only: a folder PlayMode tests point saves at, so they never read or overwrite the real save.
        private static string _testFolder;

        [SerializeField] private SaveSettings _settings;
        [SerializeField] private VoxelTerrain _pile;
        [SerializeField] private Wallet _wallet;
        [SerializeField] private NestController _nest;
        [SerializeField] private FirstPersonController _player;
        [SerializeField] private ToolBelt _toolBelt;
        [SerializeField] private Sieve _sieve;
        [SerializeField] private SieveFiller _sieveFiller;
        [SerializeField] private Basket _basket;
        [Tooltip("Every tool that can be set down in the world (the basket).")]
        [SerializeField] private PlaceableTool[] _placeableTools;
        [Tooltip("Optional: autosave waits while the game is paused.")]
        [SerializeField] private PauseController _pause;
        [Tooltip("Optional: saves the upgrades bought in the shop.")]
        [SerializeField] private Upgrades _upgrades;
        [Tooltip("Optional: saves the offerings found and where they are.")]
        [SerializeField] private Shrine _shrine;

        [Header("Debug keys")]
        [SerializeField] private InputActionReference _saveAction;
        [SerializeField] private InputActionReference _loadAction;
        [SerializeField] private InputActionReference _resetAction;

        private SaveSlot _slot;
        private SaveStateMapper _state;
        private Task _writeTask;
        private float _nextAutosaveTime;
        private bool _saveRequested;
        private bool _announceNextWrite;
        // Set when this session must not touch the save any more: it couldn't be read (so writing would
        // overwrite it), or the scene is about to reload.
        private bool _writesBlocked;
        // Nothing is saved before the save has been loaded: that would overwrite it with a fresh mountain.
        private bool _started;
        // True while a loaded save is being put back; its changes are not new progress to save.
        private bool _applying;
        // When the pending save was first asked for; an autosave waits for sliding ash to rest, but not forever.
        private float _requestedAt = -1f;

        /// <summary>Short messages for the player: saved, loaded, problems.</summary>
        public event Action<string> Notice;

        /// <summary>Full path of the save file; null before Awake.</summary>
        public string SavePath => _slot?.Store.MainPath;

        /// <summary>Main-thread time of the last save (copying the game state), for the performance overlay.</summary>
        public float LastCaptureMilliseconds { get; private set; }

        // Static state survives Play mode sessions when domain reload is off; start each session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loadOnNextStart = false;
            _testFolder = null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor only, for PlayMode tests: save controllers created from now on (this Play session) use this folder
        /// instead of the player's save folder; null goes back to normal. Reset whenever Play mode starts.
        /// </summary>
        public static void RedirectSavesForTests(string folder)
        {
            _testFolder = folder;
        }
#endif

        /// <summary>Asks for a save at the next opportunity (right away unless one is still being written).</summary>
        public void RequestSave()
        {
            _saveRequested = true;
        }

        private void Awake()
        {
            if (_settings == null || _pile == null || _wallet == null || _nest == null || _player == null ||
                _toolBelt == null || _sieve == null || _sieveFiller == null || _basket == null || _placeableTools == null)
            {
                Debug.LogError($"{nameof(SaveController)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            string folder = _testFolder ?? Path.Combine(Application.persistentDataPath, _settings.FolderName);
            _slot = new SaveSlot(new SaveFileStore(folder, _settings.FileName));
            _state = new SaveStateMapper(_pile, _wallet, _player, _toolBelt, _sieve, _sieveFiller, _basket, _placeableTools, _upgrades, _shrine);
        }

        private void OnEnable()
        {
            if (_slot == null)
            {
                return;
            }

            if (DevTools.Enabled)
            {
                EnableAction(_saveAction);
                EnableAction(_loadAction);
                EnableAction(_resetAction);
            }

            _nest.Fed += OnFed;
            if (_upgrades != null)
            {
                _upgrades.Levels.Changed += OnUpgradeChanged;
            }

            if (_shrine != null)
            {
                _shrine.Collection.Changed += OnOfferingChanged;
            }
        }

        private void OnDisable()
        {
            if (_slot == null)
            {
                return;
            }

            _nest.Fed -= OnFed;
            if (_upgrades != null)
            {
                _upgrades.Levels.Changed -= OnUpgradeChanged;
            }

            if (_shrine != null)
            {
                _shrine.Collection.Changed -= OnOfferingChanged;
            }
        }

        private void Update()
        {
            CheckWriteFinished();
            if (!_started)
            {
                return;
            }

            if (DevTools.Enabled)
            {
                HandleDebugKeys();
            }

            // Nothing changes while paused; a save that falls due then happens on resume.
            if (_pause != null && _pause.IsPaused)
            {
                return;
            }

            // Spreads the copying of changed terrain over many frames, so the save itself copies little.
            if (_writeTask == null && !_writesBlocked)
            {
                _state.WarmTerrainSnapshot(WarmChunksPerFrame, WarmAfterUnchangedFrames);
            }

            if (_settings.AutosaveInterval > 0f && Time.unscaledTime >= _nextAutosaveTime)
            {
                _saveRequested = true;
            }

            if (_saveRequested && _requestedAt < 0f)
            {
                _requestedAt = Time.unscaledTime;
            }

            if (_saveRequested && CanSaveNow() && TryStartSave())
            {
                _saveRequested = false;
                _requestedAt = -1f;
                _nextAutosaveTime = Time.unscaledTime + _settings.AutosaveInterval;
            }
        }

        // A save copies every chunk that changed since the last one; while ash slides that's many, still changing, so an
        // autosave waits for the slide to rest (up to a limit). F5 saves at once.
        private bool CanSaveNow()
        {
            return _announceNextWrite || !_pile.IsSettling || Time.unscaledTime - _requestedAt >= MaxWaitForSettling;
        }

        // Also runs when Play mode stops in the Editor.
        private void OnApplicationQuit()
        {
            if (_slot != null && _started)
            {
                SaveImmediately();
            }
        }

        // A purchase is saved right away: Glow was spent, and a crash must not lose what it bought.
        private void OnUpgradeChanged(string id)
        {
            if (!_applying)
            {
                _saveRequested = true;
            }
        }

        // A found, placed or sold offering is worth saving soon too.
        private void OnOfferingChanged(int index)
        {
            if (!_applying)
            {
                _saveRequested = true;
            }
        }

        private void OnFed(FeedResult result, IReadOnlyList<Ember> embers, Vector3 from)
        {
            if (_settings.SaveAfterFeeding)
            {
                _saveRequested = true;
            }
        }

        private void HandleDebugKeys()
        {
            if (WasPressed(_saveAction))
            {
                _saveRequested = true;
                _announceNextWrite = true;
            }
            else if (WasPressed(_loadAction))
            {
                if (!_slot.Store.HasAnySave)
                {
                    Notice?.Invoke("There's no save to load yet");
                    return;
                }

                ReloadScene(true);
            }
            else if (WasPressed(_resetAction))
            {
                StartNewGame();
            }
        }

        /// <summary>Deletes the save and reloads the scene with a fresh mountain (F8, or New game in the menu).</summary>
        public void StartNewGame()
        {
            if (_slot == null)
            {
                return;
            }

            WaitForWrite();
            _slot.Delete();
            Debug.Log("Save deleted; starting a fresh mountain.");
            ReloadScene(false);
        }

        /// <summary>Saves right away (not in the background) and quits; in the Editor, Play mode stops.</summary>
        public void SaveAndQuit()
        {
            if (_slot != null && _started)
            {
                SaveImmediately();
            }

            // Already saved; quitting must not write again.
            _writesBlocked = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ReloadScene(bool loadSave)
        {
            int buildIndex = gameObject.scene.buildIndex;
            if (buildIndex < 0)
            {
                Notice?.Invoke("This scene isn't in the Build Settings, so it can't be reloaded");
                return;
            }

            WaitForWrite();
            _writesBlocked = true;
            _loadOnNextStart = loadSave;
            SceneManager.LoadScene(buildIndex);
        }

        /// <summary>
        /// Applies the save on top of the freshly built mountain and starts autosaving. Called once by the
        /// game's bootstrap, after the mountain is built.
        /// </summary>
        public void LoadAtStart()
        {
            if (_slot == null || _started)
            {
                return;
            }

            _started = true;
            Debug.Log($"Save file: {SavePath}");
            _nextAutosaveTime = Time.unscaledTime + _settings.AutosaveInterval;
            LoadSave();

            // A throwaway capture behind the loading screen. The first run of this code is compiled on the spot
            // (6 ms measured), and it fills the terrain snapshot with every chunk the save restored, so neither
            // cost lands on the first autosave in play.
            Capture(out _);
            LastCaptureMilliseconds = 0f;
        }

        private void LoadSave()
        {
            bool forced = _loadOnNextStart;
            _loadOnNextStart = false;
            if (Application.isEditor && !_settings.LoadSaveInEditor && !forced)
            {
                Debug.Log("Load Save In Editor is off: starting a fresh mountain.");
                return;
            }

            bool loaded;
            SaveFile file;
            TerrainSnapshot terrain;
            string warning;
            try
            {
                loaded = _slot.TryLoad(out file, out terrain, out warning);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Couldn't even open it (locked, no permission): leave it alone rather than overwrite it.
                _writesBlocked = true;
                Debug.LogError($"Couldn't open the save at {SavePath}: {e}", this);
                Notice?.Invoke("The save couldn't be opened; saving is off until the game restarts");
                return;
            }

            if (warning != null)
            {
                Debug.LogWarning(warning, this);
                Notice?.Invoke(warning);
            }

            if (loaded)
            {
                Apply(file, terrain);
            }
        }

        private void Apply(SaveFile file, TerrainSnapshot terrain)
        {
            bool sameMountain;
            int lostBasketEmbers;
            _applying = true;
            try
            {
                sameMountain = _state.Apply(file, terrain, out lostBasketEmbers);
            }
            finally
            {
                _applying = false;
            }

            if (lostBasketEmbers > 0)
            {
                Debug.LogWarning($"{lostBasketEmbers} saved embers didn't fit in the basket and were lost.", this);
            }

            if (sameMountain)
            {
                Notice?.Invoke("Save loaded");
                return;
            }

            const string message = "The mountain's settings changed since this save, so it was regenerated. Glow, Heat, open layers and tools were kept.";
            Debug.LogWarning(message, this);
            Notice?.Invoke(message);
        }

        // Starts a background write of the current state; false if the previous one is still running.
        private bool TryStartSave()
        {
            if (_writesBlocked)
            {
                return true;
            }

            if (_writeTask != null && !_writeTask.IsCompleted)
            {
                return false;
            }

            SaveFile file = Capture(out TerrainSnapshot terrain);
            SaveSlot slot = _slot;
            _writeTask = Task.Run(() => slot.Write(file, terrain));
            return true;
        }

        private void SaveImmediately()
        {
            if (_writesBlocked)
            {
                return;
            }

            WaitForWrite();
            try
            {
                SaveFile file = Capture(out TerrainSnapshot terrain);
                _slot.Write(file, terrain);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"Saving to {SavePath} failed: {e}", this);
            }
        }

        private void WaitForWrite()
        {
            if (_writeTask == null)
            {
                return;
            }

            try
            {
                _writeTask.Wait();
            }
            catch (AggregateException)
            {
                // Reported by CheckWriteFinished.
            }

            CheckWriteFinished();
        }

        private void CheckWriteFinished()
        {
            if (_writeTask == null || !_writeTask.IsCompleted)
            {
                return;
            }

            if (_writeTask.IsFaulted)
            {
                Exception error = _writeTask.Exception?.GetBaseException();
                Debug.LogError($"Saving to {SavePath} failed: {error}", this);
                Notice?.Invoke("Saving failed: " + error?.Message);
            }
            else if (_announceNextWrite)
            {
                Notice?.Invoke("Game saved");
            }

            _announceNextWrite = false;
            _writeTask = null;
        }

        private SaveFile Capture(out TerrainSnapshot terrain)
        {
            // A hole still opening counts as open: its ash is already in the sieve. Not timed: it is the scoop's work.
            _pile.CompleteScoopNow();
            long started = TimeMeasure.Now();
            using (CaptureMarker.Auto())
            {
                SaveFile file = _state.Capture(out terrain);
                LastCaptureMilliseconds = TimeMeasure.MillisecondsSince(started);
                return file;
            }
        }

        private static void EnableAction(InputActionReference action)
        {
            if (action != null)
            {
                action.action.Enable();
            }
        }

        private static bool WasPressed(InputActionReference action)
        {
            return action != null && action.action.WasPressedThisFrame();
        }
    }
}
