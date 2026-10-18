using System;
using System.IO;
using Cinderbird.Core;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Helpers;
using Cinderbird.Nest;
using Cinderbird.Player;
using Cinderbird.Powers;
using Cinderbird.Terrain;
using Cinderbird.UI;
using UnityEngine;

namespace Cinderbird.Pacing
{
    /// <summary>
    /// The pacing log for playtests: once a minute of play, a <c>[Pace]</c> line with the ash moved and embers dug (you,
    /// Sally, the wind), embers fed, Glow earned and spent, and how quickly your scoops follow each other; plus a line for
    /// each Heat level, layer opening and purchase. Appended to pace.log (next to the save) across sessions, on the play
    /// clock, and to the Player.log. A testing aid: only where <see cref="DevTools.Enabled"/>.
    /// </summary>
    public sealed class PaceLog : MonoBehaviour
    {
        private const string FileName = "pace.log";

        [SerializeField] private PlayClock _clock;
        [SerializeField] private ScoopInteractor _scoop;
        [SerializeField] private NestController _nest;
        [SerializeField] private NestHeat _heat;
        [SerializeField] private Wallet _wallet;
        [SerializeField] private VoxelTerrain _pile;
        [Tooltip("Optional: the helper's scoops.")]
        [SerializeField] private Sally _sally;
        [Tooltip("Optional: the Warm Wind's gusts.")]
        [SerializeField] private WarmWind _warmWind;
        [Tooltip("Optional: what the player buys.")]
        [SerializeField] private ShopView _shop;
        [Tooltip("Optional: says in the log whether Test Mode was on (its numbers aren't real play).")]
        [SerializeField] private TestModeSettings _testMode;
        [Tooltip("Seconds of play per line.")]
        [SerializeField, Min(10f)] private float _interval = 60f;

        private PaceTracker _tracker;
        private string _path;
        private int _lastGlow;

        private void Awake()
        {
            if (!DevTools.Enabled)
            {
                enabled = false;
                return;
            }

            if (_clock == null || _scoop == null || _nest == null || _heat == null || _wallet == null || _pile == null)
            {
                Debug.LogError($"{nameof(PaceLog)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _path = Path.Combine(Application.persistentDataPath, FileName);
        }

        private void Start()
        {
            if (!enabled)
            {
                return;
            }

            // After the bootstrap has loaded the save, so the play clock and Glow are the game's, not zero.
            _tracker = new PaceTracker(_interval, _clock.Seconds);
            _lastGlow = _wallet.Currencies.Glow;
            Write(PaceText.Event(_clock.Seconds, $"session start · {Application.version} · Test Mode {(TestModeOn() ? "ON" : "off")}"));
            Subscribe(true);
        }

        private void OnDestroy()
        {
            if (_tracker != null)
            {
                Subscribe(false);
            }
        }

        private void Subscribe(bool on)
        {
            if (on)
            {
                _scoop.Scooped += OnPlayerScooped;
                _nest.Fed += OnFed;
                _heat.LevelReached += OnLevelReached;
                _pile.LayerUnlocked += OnLayerUnlocked;
                _wallet.Currencies.Changed += OnCurrenciesChanged;
            }
            else
            {
                _scoop.Scooped -= OnPlayerScooped;
                _nest.Fed -= OnFed;
                _heat.LevelReached -= OnLevelReached;
                _pile.LayerUnlocked -= OnLayerUnlocked;
                _wallet.Currencies.Changed -= OnCurrenciesChanged;
            }

            if (_sally != null)
            {
                if (on) _sally.Scooped += OnHelperScooped; else _sally.Scooped -= OnHelperScooped;
            }

            if (_warmWind != null)
            {
                if (on) _warmWind.Gusted += OnGusted; else _warmWind.Gusted -= OnGusted;
            }

            if (_shop != null)
            {
                if (on) _shop.Purchased += OnPurchased; else _shop.Purchased -= OnPurchased;
            }
        }

        private void Update()
        {
            if (_tracker == null || !_tracker.TryClose(_clock.Seconds, out PaceWindow window))
            {
                return;
            }

            Write(PaceText.Window(_clock.Seconds, window, _tracker.Session, Status()));
        }

        private PaceStatus Status()
        {
            AshTerrain terrain = _pile.Terrain;
            int layer = terrain != null ? terrain.Locks.DeepestUnlocked : 0;
            float cleared = terrain != null ? terrain.ClearedShare(layer) : 0f;
            int heat = _wallet.Currencies.Heat;
            int level = _heat.Level;
            int next = level < _heat.Levels.MaxLevel ? _heat.Levels.HeatFor(level + 1) : 0;
            return new PaceStatus(_pile.LayerName(layer), cleared, heat, level, next, _wallet.Currencies.Glow);
        }

        private void OnPlayerScooped(DigResult result)
        {
            _tracker.Dig(PaceSource.Player, result.RemovedVolume, result.EmberCount, _clock.Seconds);
        }

        private void OnHelperScooped(DigResult result)
        {
            _tracker.Dig(PaceSource.Helper, result.RemovedVolume, result.EmberCount, _clock.Seconds);
        }

        private void OnGusted(DigResult result)
        {
            _tracker.Dig(PaceSource.Wind, result.RemovedVolume, result.EmberCount, _clock.Seconds);
        }

        private void OnFed(Nest.FeedResult result, System.Collections.Generic.IReadOnlyList<Ember> embers, Vector3 from)
        {
            _tracker.Fed(result.EmberCount);
        }

        private void OnCurrenciesChanged()
        {
            int glow = _wallet.Currencies.Glow;
            // A loaded save sets Glow directly: that was neither earned nor spent.
            if (!_wallet.Currencies.IsRestoring)
            {
                _tracker.GlowChanged(glow - _lastGlow);
            }

            _lastGlow = glow;
        }

        private void OnLevelReached(int level)
        {
            Write(PaceText.Event(_clock.Seconds, $"Heat level {level}"));
        }

        private void OnLayerUnlocked(int layer)
        {
            Write(PaceText.Event(_clock.Seconds, $"{_pile.LayerName(layer)} opened"));
        }

        private void OnPurchased(UpgradeDefinition upgrade, int level)
        {
            _tracker.Purchased();
            Write(PaceText.Event(_clock.Seconds, $"bought {upgrade.LevelName(level)}"));
        }

        private bool TestModeOn()
        {
            return _testMode != null && _testMode.Active;
        }

        private void Write(string line)
        {
            Debug.Log(line);
            try
            {
                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"Pacing log not written: {exception.Message}");
            }
        }
    }
}
