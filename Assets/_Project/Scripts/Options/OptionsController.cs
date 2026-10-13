using System;
using System.Collections.Generic;
using System.IO;
using Cinderbird.Player;
using Cinderbird.Save;
using UnityEngine;

namespace Cinderbird.Options
{
    /// <summary>
    /// Thin adapter: loads options.json on start, applies the options to the game (mouse, camera, quality,
    /// VSync and frame cap, window, volume) the moment they change, and writes the file when the options panel
    /// closes or the game quits.
    /// </summary>
    public sealed class OptionsController : MonoBehaviour
    {
        [SerializeField] private OptionsSettings _settings;
        [SerializeField] private FirstPersonController _player;
        [Tooltip("The world camera; the held-items overlay camera keeps its own field of view.")]
        [SerializeField] private Camera _camera;

        private OptionsStore _store;
        private OptionLimits _limits;
        private GameOptions _current;
        private List<Vector2Int> _resolutions;
        private bool _unsaved;

        /// <summary>Fired after any option changed and was applied.</summary>
        public event Action Changed;

        /// <summary>The options in effect. Read it freely; change it only through <see cref="Change"/>.</summary>
        public GameOptions Current => _current;

        public OptionLimits Limits => _limits;

        /// <summary>Window sizes the display supports, smallest first.</summary>
        public IReadOnlyList<Vector2Int> Resolutions => _resolutions;

        private void Awake()
        {
            if (_settings == null || _player == null || _camera == null)
            {
                Debug.LogError($"{nameof(OptionsController)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _limits = _settings.CreateLimits(Math.Max(1, QualitySettings.names.Length));
            _resolutions = ResolutionList.Unique(DisplaySizes());
            _store = new OptionsStore(new SaveFileStore(Application.persistentDataPath, _settings.FileName));

            GameOptions defaults = GameOptions.CreateDefaults(_limits);
            try
            {
                _current = _store.Load(defaults, out string warning);
                if (warning != null)
                {
                    Debug.LogWarning(warning, this);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"Couldn't read the options at {_store.Store.MainPath}: {e}", this);
                _current = defaults;
            }

            _current.Sanitize(_limits);
            Apply();
        }

        /// <summary>Edits the options, keeps them in range and applies them at once. Written to disk by <see cref="SaveIfChanged"/>.</summary>
        public void Change(Action<GameOptions> edit)
        {
            if (_current == null)
            {
                return;
            }

            edit(_current);
            _current.Sanitize(_limits);
            _unsaved = true;
            Apply();
            Changed?.Invoke();
        }

        public void ResetToDefaults()
        {
            Change(options => options.CopyFrom(GameOptions.CreateDefaults(_limits)));
        }

        /// <summary>Writes options.json if anything changed since the last write. Small file, so done right away.</summary>
        public void SaveIfChanged()
        {
            if (!_unsaved || _store == null)
            {
                return;
            }

            try
            {
                _store.Save(_current);
                _unsaved = false;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"Couldn't write the options to {_store.Store.MainPath}: {e}", this);
            }
        }

        private void OnApplicationQuit()
        {
            SaveIfChanged();
        }

        private void Apply()
        {
            _player.LookSensitivityMultiplier = _current.MouseSensitivity;
            _player.InvertY = _current.InvertY;
            _camera.fieldOfView = _current.FieldOfView;
            AudioListener.volume = _current.MasterVolume;

            // Switching the level swaps the URP asset; the level carries its own VSync setting, so set ours after.
            if (QualitySettings.GetQualityLevel() != _current.QualityLevel)
            {
                QualitySettings.SetQualityLevel(_current.QualityLevel, true);
            }

            QualitySettings.vSyncCount = _current.VSync ? 1 : 0;
            // -1 is the platform default (no cap); VSync already caps at the monitor's refresh rate.
            Application.targetFrameRate = _current.VSync || _current.FrameRateLimit <= 0 ? -1 : _current.FrameRateLimit;

            ApplyDisplay();
        }

        // The Editor's Game view ignores resolution and fullscreen requests; they take effect in a build.
        private void ApplyDisplay()
        {
            if (Application.isEditor)
            {
                return;
            }

            Resolution native = Screen.currentResolution;
            int width = _current.HasResolution ? _current.ResolutionWidth : native.width;
            int height = _current.HasResolution ? _current.ResolutionHeight : native.height;
            FullScreenMode mode = ToFullScreenMode(_current.DisplayMode);
            if (Screen.width != width || Screen.height != height || Screen.fullScreenMode != mode)
            {
                Screen.SetResolution(width, height, mode);
            }
        }

        private static FullScreenMode ToFullScreenMode(DisplayModeOption mode)
        {
            switch (mode)
            {
                case DisplayModeOption.Fullscreen:
                    return FullScreenMode.ExclusiveFullScreen;
                case DisplayModeOption.Windowed:
                    return FullScreenMode.Windowed;
                default:
                    return FullScreenMode.FullScreenWindow;
            }
        }

        private static IEnumerable<Vector2Int> DisplaySizes()
        {
            foreach (Resolution resolution in Screen.resolutions)
            {
                yield return new Vector2Int(resolution.width, resolution.height);
            }
        }
    }
}
