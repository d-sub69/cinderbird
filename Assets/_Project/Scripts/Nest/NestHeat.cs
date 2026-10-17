using System;
using Cinderbird.Economy;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>
    /// Thin adapter for the nest's Heat levels and what they open: works out the level from the Heat earned,
    /// announces new levels, and melts the next ash layer open as soon as <see cref="LayerUnlockRule"/> allows
    /// (checked whenever Heat changes or embers leave the pile).
    /// </summary>
    public sealed class NestHeat : MonoBehaviour
    {
        [SerializeField] private HeatLevelTable _levelTable;
        [SerializeField] private Wallet _wallet;
        [SerializeField] private VoxelTerrain _pile;
        [SerializeField] private LayerCatalog _layers;
        [Tooltip("Optional: the shop puts upgrades that come with a Heat level on sale (a power's).")]
        [SerializeField] private Upgrades _upgrades;

        private HeatLevels _levels;
        private float[] _cleared;

        /// <summary>Fired when the Heat reaches a new level by playing (not when a save sets it).</summary>
        public event Action<int> LevelReached;

        /// <summary>The current Heat level; 0 before level 1.</summary>
        public int Level { get; private set; }

        public HeatLevels Levels => _levels;

        public int Heat => _wallet != null ? _wallet.Currencies.Heat : 0;

        /// <summary>The next layer still sealed, or -1 when every layer is open.</summary>
        public int NextLayer
        {
            get
            {
                int next = _pile.DeepestUnlockedLayer + 1;
                return next < _pile.LayerCount ? next : -1;
            }
        }

        public int RequiredLevelFor(int layer)
        {
            return _layers.UnlockHeatLevelOf(layer);
        }

        public float RequiredClearedShareFor(int layer)
        {
            return _layers.UnlockClearedShareOf(layer);
        }

        /// <summary>The least cleared layer above <paramref name="layer"/> and its share: what holds it shut.</summary>
        public float LeastClearedAbove(int layer, out int leastLayer)
        {
            FillClearedShares();
            return LayerUnlockRule.LeastClearedAbove(layer, _cleared, out leastLayer);
        }

        private void Awake()
        {
            if (_levelTable == null || _wallet == null || _pile == null || _layers == null)
            {
                Debug.LogError($"{nameof(NestHeat)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _levels = _levelTable.CreateLevels();
            _cleared = new float[Mathf.Max(1, _layers.Count)];
        }

        private void OnEnable()
        {
            if (_levels == null)
            {
                return;
            }

            _wallet.Currencies.Changed += OnHeatChanged;
            _wallet.Currencies.Restored += OnRestored;
            _pile.Dug += OnDug;
            _pile.Restored += OnRestored;
            _pile.EmbersCollected += TryOpenLayers;
            OnRestored();
        }

        private void OnDisable()
        {
            if (_levels == null)
            {
                return;
            }

            _wallet.Currencies.Changed -= OnHeatChanged;
            _wallet.Currencies.Restored -= OnRestored;
            _pile.Dug -= OnDug;
            _pile.Restored -= OnRestored;
            _pile.EmbersCollected -= TryOpenLayers;
        }

        private void OnHeatChanged()
        {
            // A save setting the Heat is not a level reached by playing.
            UpdateLevel(!_wallet.Currencies.IsRestoring);
            TryOpenLayers();
        }

        // A save set the Heat (or the terrain) directly: catch up quietly.
        private void OnRestored()
        {
            UpdateLevel(false);
            TryOpenLayers();
        }

        private void OnDug(DigResult result, Vector3 center, float radius)
        {
            if (result.EmberCount > 0)
            {
                TryOpenLayers();
            }
        }

        private void UpdateLevel(bool announce)
        {
            int level = _levels.LevelFor(Heat);
            if (level == Level)
            {
                return;
            }

            bool risen = level > Level;
            Level = level;
            if (_upgrades != null && _upgrades.Shop != null)
            {
                _upgrades.Shop.SetHeatLevel(level);
            }

            if (announce && risen)
            {
                LevelReached?.Invoke(level);
            }
        }

        // Opens layers one at a time while the rule allows, so a big jump in Heat can open several.
        private void TryOpenLayers()
        {
            if (_pile.Terrain == null)
            {
                return;
            }

            while (true)
            {
                int next = NextLayer;
                if (next < 0)
                {
                    return;
                }

                FillClearedShares();
                if (!LayerUnlockRule.CanOpen(next, Level, RequiredLevelFor(next), RequiredClearedShareFor(next), _cleared)
                    || _pile.UnlockNextLayer() < 0)
                {
                    return;
                }
            }
        }

        private void FillClearedShares()
        {
            AshTerrain terrain = _pile.Terrain;
            for (int i = 0; i < _cleared.Length; i++)
            {
                _cleared[i] = terrain != null ? terrain.ClearedShare(i) : 0f;
            }
        }
    }
}
