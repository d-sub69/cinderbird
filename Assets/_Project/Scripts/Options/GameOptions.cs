using System;
using UnityEngine;

namespace Cinderbird.Options
{
    /// <summary>
    /// The player's options, as written to options.json (separate from the game save, so a new game keeps them).
    /// Loading starts from the defaults and overwrites only the keys the file has, so options added later get
    /// their default in older files.
    /// </summary>
    [Serializable]
    public sealed class GameOptions
    {
        public const int CurrentVersion = 1;

        // Field names are the JSON keys.
        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private float _mouseSensitivity = 1f;
        [SerializeField] private bool _invertY;
        [SerializeField] private float _fieldOfView = 60f;
        [SerializeField] private DisplayModeOption _displayMode = DisplayModeOption.Borderless;
        [SerializeField] private int _resolutionWidth;
        [SerializeField] private int _resolutionHeight;
        [SerializeField] private bool _vSync = true;
        [SerializeField] private int _frameRateLimit;
        [SerializeField] private int _qualityLevel;
        [SerializeField] private float _masterVolume = 1f;

        public int Version { get => _version; set => _version = value; }

        /// <summary>Multiplier on the base look sensitivity; 1 is the designed feel.</summary>
        public float MouseSensitivity { get => _mouseSensitivity; set => _mouseSensitivity = value; }

        public bool InvertY { get => _invertY; set => _invertY = value; }

        /// <summary>Vertical field of view of the world camera, in degrees.</summary>
        public float FieldOfView { get => _fieldOfView; set => _fieldOfView = value; }

        public DisplayModeOption DisplayMode { get => _displayMode; set => _displayMode = value; }

        /// <summary>Window or screen width in pixels; 0 means the display's own resolution.</summary>
        public int ResolutionWidth { get => _resolutionWidth; set => _resolutionWidth = value; }

        public int ResolutionHeight { get => _resolutionHeight; set => _resolutionHeight = value; }

        public bool HasResolution => _resolutionWidth > 0 && _resolutionHeight > 0;

        public bool VSync { get => _vSync; set => _vSync = value; }

        /// <summary>Frames per second cap while VSync is off; 0 means no limit.</summary>
        public int FrameRateLimit { get => _frameRateLimit; set => _frameRateLimit = value; }

        /// <summary>Index into the project's quality levels (Low, Medium, High).</summary>
        public int QualityLevel { get => _qualityLevel; set => _qualityLevel = value; }

        /// <summary>0 (silent) to 1 (full).</summary>
        public float MasterVolume { get => _masterVolume; set => _masterVolume = value; }

        public static GameOptions CreateDefaults(OptionLimits limits)
        {
            return new GameOptions
            {
                _mouseSensitivity = limits.DefaultSensitivity,
                _fieldOfView = limits.DefaultFieldOfView,
                _qualityLevel = limits.DefaultQualityLevel,
            };
        }

        public GameOptions Clone()
        {
            return (GameOptions)MemberwiseClone();
        }

        /// <summary>Takes over every value of <paramref name="other"/>, e.g. to reset to the defaults in place.</summary>
        public void CopyFrom(GameOptions other)
        {
            _version = other._version;
            _mouseSensitivity = other._mouseSensitivity;
            _invertY = other._invertY;
            _fieldOfView = other._fieldOfView;
            _displayMode = other._displayMode;
            _resolutionWidth = other._resolutionWidth;
            _resolutionHeight = other._resolutionHeight;
            _vSync = other._vSync;
            _frameRateLimit = other._frameRateLimit;
            _qualityLevel = other._qualityLevel;
            _masterVolume = other._masterVolume;
        }

        /// <summary>Pulls every value back into its allowed range; a hand-edited or old file can't break the game.</summary>
        public void Sanitize(OptionLimits limits)
        {
            _version = CurrentVersion;
            _mouseSensitivity = ClampOrDefault(_mouseSensitivity, limits.MinSensitivity, limits.MaxSensitivity, limits.DefaultSensitivity);
            _fieldOfView = ClampOrDefault(_fieldOfView, limits.MinFieldOfView, limits.MaxFieldOfView, limits.DefaultFieldOfView);
            _masterVolume = ClampOrDefault(_masterVolume, 0f, 1f, 1f);

            if (!Enum.IsDefined(typeof(DisplayModeOption), _displayMode))
            {
                _displayMode = DisplayModeOption.Borderless;
            }

            if (_resolutionWidth <= 0 || _resolutionHeight <= 0)
            {
                _resolutionWidth = 0;
                _resolutionHeight = 0;
            }

            _frameRateLimit = Math.Max(0, _frameRateLimit);
            if (_qualityLevel < 0 || _qualityLevel >= limits.QualityLevelCount)
            {
                _qualityLevel = limits.DefaultQualityLevel;
            }
        }

        // NaN (e.g. from a hand-edited file) fails every comparison, so it gets the default instead of passing through.
        private static float ClampOrDefault(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Math.Min(Math.Max(value, min), max);
        }
    }
}
