using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Nest;
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
    /// Thin adapter between the game and its <see cref="SaveSlot"/>: loads the save at start, autosaves on a
    /// timer, after feeding the nest and on quit, and handles the debug keys (F5 save, F9 reload the save,
    /// F8 delete it and start fresh). Saving copies the game state on the main thread (quick) and packs and
    /// writes it on a background thread, so autosaves don't stall a frame.
    /// </summary>
    // Runs its Start after everyone else's, so the mountain, the nest's Heat goal and the tools are all set up
    // before the save is applied on top.
    [DefaultExecutionOrder(100)]
    public sealed class SaveController : MonoBehaviour
    {
        private static readonly ProfilerMarker CaptureMarker = new ProfilerMarker("Cinderbird.Save");

        // F9 reloads the scene to load the save; this makes that one start load it even when the Editor
        // setting says to start fresh. Static because the reload replaces this object.
        private static bool _loadOnNextStart;

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

        [Header("Debug keys")]
        [SerializeField] private InputActionReference _saveAction;
        [SerializeField] private InputActionReference _loadAction;
        [SerializeField] private InputActionReference _resetAction;

        private readonly List<ToolDefinition> _slotBuffer = new List<ToolDefinition>();
        private readonly List<ToolDefinition> _placedBuffer = new List<ToolDefinition>();
        private SaveSlot _slot;
        private Task _writeTask;
        private float _nextAutosaveTime;
        private bool _saveRequested;
        private bool _announceNextWrite;
        // Set when this session must not touch the save any more: it couldn't be read (so writing would
        // overwrite it), or the scene is about to reload.
        private bool _writesBlocked;

        /// <summary>Short messages for the player: saved, loaded, problems.</summary>
        public event Action<string> Notice;

        /// <summary>Full path of the save file; null before Awake.</summary>
        public string SavePath => _slot?.Store.MainPath;

        // Static state survives Play mode sessions when domain reload is off; start each session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loadOnNextStart = false;
        }

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

            string folder = Path.Combine(Application.persistentDataPath, _settings.FolderName);
            _slot = new SaveSlot(new SaveFileStore(folder, _settings.FileName));
        }

        private void OnEnable()
        {
            if (_slot == null)
            {
                return;
            }

            EnableAction(_saveAction);
            EnableAction(_loadAction);
            EnableAction(_resetAction);
            _nest.Fed += OnFed;
        }

        private void OnDisable()
        {
            if (_slot == null)
            {
                return;
            }

            _nest.Fed -= OnFed;
        }

        private void Start()
        {
            Debug.Log($"Save file: {SavePath}");
            LoadAtStart();
            _nextAutosaveTime = Time.unscaledTime + _settings.AutosaveInterval;
        }

        private void Update()
        {
            CheckWriteFinished();
            HandleDebugKeys();

            if (_settings.AutosaveInterval > 0f && Time.unscaledTime >= _nextAutosaveTime)
            {
                _saveRequested = true;
            }

            if (_saveRequested && TryStartSave())
            {
                _saveRequested = false;
                _nextAutosaveTime = Time.unscaledTime + _settings.AutosaveInterval;
            }
        }

        // Also runs when Play mode stops in the Editor.
        private void OnApplicationQuit()
        {
            if (_slot != null)
            {
                SaveImmediately();
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
                WaitForWrite();
                _slot.Delete();
                Debug.Log("Save deleted; starting a fresh mountain.");
                ReloadScene(false);
            }
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

        private void LoadAtStart()
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
            AshTerrain ash = _pile.Terrain;
            bool sameMountain = ash != null && file.Terrain.Fingerprint == _pile.Fingerprint &&
                                (terrain == null || terrain.FitsWorld(ash.World));
            if (ash != null)
            {
                ash.Locks.UnlockThrough(file.Terrain.DeepestUnlockedLayer);
                if (sameMountain && terrain != null)
                {
                    terrain.ApplyTo(ash);
                }

                _pile.CommitRestore();
            }

            _wallet.Currencies.Restore(Math.Max(0, file.Currencies.Glow), Math.Max(0, file.Currencies.Heat));
            RestoreSieve(file.Sieve);
            int dropped = _basket.Contents.Restore(EmberRecord.ToEmbers(file.Basket.Embers));
            if (dropped > 0)
            {
                Debug.LogWarning($"{dropped} saved embers didn't fit in the basket and were lost.", this);
            }

            // On a regenerated mountain the saved spots may be inside the ash: placed tools come back to the
            // hands and the player starts at the default spot.
            RestoreTools(file.Tools, sameMountain);
            if (sameMountain)
            {
                _player.RestorePose(file.Player.Pose);
                Notice?.Invoke("Save loaded");
            }
            else
            {
                const string message = "The mountain's settings changed since this save, so it was regenerated. Glow, Heat, open layers and tools were kept.";
                Debug.LogWarning(message, this);
                Notice?.Invoke(message);
            }
        }

        private void RestoreSieve(SieveSave saved)
        {
            _sieve.Contents.Restore(Mathf.Max(0f, saved.AshVolume), saved.LoadVolume, saved.AshColor, saved.DustColor,
                EmberRecord.ToEmbers(saved.Embers));
        }

        private void RestoreTools(ToolsSave saved, bool restorePlaced)
        {
            _placedBuffer.Clear();
            if (restorePlaced)
            {
                foreach (PlacedToolSave placed in saved.Placed)
                {
                    PlaceableTool tool = FindPlaceable(placed.ToolId);
                    if (tool != null && !_placedBuffer.Contains(tool.Tool))
                    {
                        _placedBuffer.Add(tool.Tool);
                    }
                }
            }

            _slotBuffer.Clear();
            foreach (string id in saved.Slots)
            {
                ToolDefinition tool = _toolBelt.FindTool(id);
                // A tool can't be both in a slot and out in the world.
                _slotBuffer.Add(tool != null && !_placedBuffer.Contains(tool) ? tool : null);
            }

            _toolBelt.RestoreSlots(_slotBuffer, saved.ActiveSlot, _placedBuffer);

            if (!restorePlaced)
            {
                return;
            }

            foreach (PlacedToolSave placed in saved.Placed)
            {
                PlaceableTool tool = FindPlaceable(placed.ToolId);
                if (tool != null && !tool.IsPlaced)
                {
                    tool.PlaceAt(placed.Position - Vector3.up * tool.PlacedHeightOffset, placed.Rotation);
                }
            }
        }

        private PlaceableTool FindPlaceable(string toolId)
        {
            foreach (PlaceableTool tool in _placeableTools)
            {
                if (tool != null && ToolBelt.IdOf(tool.Tool) == toolId)
                {
                    return tool;
                }
            }

            return null;
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
            using (CaptureMarker.Auto())
            {
                SaveFile file = new SaveFile
                {
                    SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                };

                AshTerrain ash = _pile.Terrain;
                terrain = ash != null ? TerrainSnapshot.Capture(ash) : null;
                file.Terrain.Fingerprint = _pile.Fingerprint ?? string.Empty;
                file.Terrain.DeepestUnlockedLayer = _pile.DeepestUnlockedLayer;

                file.Currencies.Glow = _wallet.Currencies.Glow;
                file.Currencies.Heat = _wallet.Currencies.Heat;
                file.Player.Pose = _player.Pose;
                CaptureTools(file.Tools);
                CaptureSieve(file.Sieve);
                EmberRecord.Fill(file.Basket.Embers, _basket.Contents.Embers);
                return file;
            }
        }

        private void CaptureTools(ToolsSave save)
        {
            Inventory<ToolDefinition> inventory = _toolBelt.Inventory;
            for (int i = 0; i < inventory.Capacity; i++)
            {
                save.Slots.Add(ToolBelt.IdOf(inventory[i]));
            }

            save.ActiveSlot = inventory.ActiveSlot;
            foreach (PlaceableTool tool in _placeableTools)
            {
                if (tool != null && tool.IsPlaced)
                {
                    Transform pose = tool.transform;
                    save.Placed.Add(new PlacedToolSave(ToolBelt.IdOf(tool.Tool), pose.position, pose.rotation));
                }
            }
        }

        private void CaptureSieve(SieveSave save)
        {
            SieveContents contents = _sieve.Contents;
            EmberRecord.Fill(save.Embers, contents.Embers);
            if (!_sieveFiller.IsFilling)
            {
                save.AshVolume = contents.AshVolume;
                save.LoadVolume = contents.LoadVolume;
                save.AshColor = contents.AshColor;
                save.DustColor = contents.DustColor;
                return;
            }

            // A load still pouring in already left the mountain, so it counts as in the sieve. Pours only start
            // into an ash-free sieve, so it is the whole load.
            IReadOnlyList<Ember> incoming = _sieveFiller.IncomingEmbers;
            for (int i = 0; i < incoming.Count; i++)
            {
                save.Embers.Add(new EmberRecord(incoming[i]));
            }

            save.AshVolume = contents.AshVolume + _sieveFiller.IncomingVolume;
            save.LoadVolume = save.AshVolume;
            save.AshColor = _sieveFiller.IncomingAshColor;
            save.DustColor = _sieveFiller.IncomingDustColor;
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
