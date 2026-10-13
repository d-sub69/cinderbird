using System.Collections.Generic;
using System.Globalization;
using Cinderbird.Options;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// The options panel inside the pause menu. Every control applies its option at once; the file is written
    /// when the panel closes (Back, Esc or resuming).
    /// </summary>
    public sealed class OptionsMenuView : MonoBehaviour
    {
        // Same order as DisplayModeOption.
        private static readonly string[] DisplayModeNames = { "Borderless window", "Fullscreen", "Windowed" };

        [SerializeField] private OptionsController _options;

        [Header("Controls")]
        [SerializeField] private Slider _sensitivity;
        [SerializeField] private TMP_Text _sensitivityValue;
        [SerializeField] private Toggle _invertY;
        [SerializeField] private Slider _fieldOfView;
        [SerializeField] private TMP_Text _fieldOfViewValue;

        [Header("Display")]
        [SerializeField] private TMP_Dropdown _displayMode;
        [SerializeField] private TMP_Dropdown _resolution;
        [SerializeField] private Toggle _vSync;
        [SerializeField] private TMP_Dropdown _frameRateLimit;
        [SerializeField] private TMP_Dropdown _quality;

        [Header("Sound")]
        [SerializeField] private Slider _volume;
        [SerializeField] private TMP_Text _volumeValue;

        [Header("Buttons")]
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _backButton;

        // Frame caps behind the dropdown entries, in order; 0 = Unlimited (the last entry).
        private readonly List<int> _frameRateValues = new List<int>();
        private bool _ready;

        private void Awake()
        {
            if (_options == null || _sensitivity == null || _sensitivityValue == null || _invertY == null ||
                _fieldOfView == null || _fieldOfViewValue == null || _displayMode == null || _resolution == null ||
                _vSync == null || _frameRateLimit == null || _quality == null || _volume == null || _volumeValue == null ||
                _resetButton == null || _backButton == null)
            {
                Debug.LogError($"{nameof(OptionsMenuView)} on '{name}' has unassigned references.", this);
                return;
            }

            _ready = true;
        }

        // Start, not Awake: the options controller reads the display and the file in its Awake.
        private void Start()
        {
            // The controller has no options when its own setup failed (it logs why).
            if (!_ready || _options.Current == null)
            {
                _ready = false;
                return;
            }

            FillChoices();
            _sensitivity.onValueChanged.AddListener(value => _options.Change(o => o.MouseSensitivity = value));
            _invertY.onValueChanged.AddListener(value => _options.Change(o => o.InvertY = value));
            _fieldOfView.onValueChanged.AddListener(value => _options.Change(o => o.FieldOfView = value));
            _displayMode.onValueChanged.AddListener(index => _options.Change(o => o.DisplayMode = (DisplayModeOption)index));
            _resolution.onValueChanged.AddListener(OnResolutionChosen);
            _vSync.onValueChanged.AddListener(value => _options.Change(o => o.VSync = value));
            _frameRateLimit.onValueChanged.AddListener(index => _options.Change(o => o.FrameRateLimit = _frameRateValues[index]));
            _quality.onValueChanged.AddListener(index => _options.Change(o => o.QualityLevel = index));
            _volume.onValueChanged.AddListener(value => _options.Change(o => o.MasterVolume = value));
            _resetButton.onClick.AddListener(_options.ResetToDefaults);
            _backButton.onClick.AddListener(() => gameObject.SetActive(false));
            Refresh();
        }

        private void OnEnable()
        {
            if (!_ready)
            {
                return;
            }

            _options.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (!_ready)
            {
                return;
            }

            _options.Changed -= Refresh;
            _options.SaveIfChanged();
        }

        private void FillChoices()
        {
            OptionLimits limits = _options.Limits;
            _sensitivity.minValue = limits.MinSensitivity;
            _sensitivity.maxValue = limits.MaxSensitivity;
            _fieldOfView.minValue = limits.MinFieldOfView;
            _fieldOfView.maxValue = limits.MaxFieldOfView;
            _fieldOfView.wholeNumbers = true;
            _volume.minValue = 0f;
            _volume.maxValue = 1f;

            _displayMode.ClearOptions();
            _displayMode.AddOptions(new List<string>(DisplayModeNames));

            List<string> sizes = new List<string>();
            foreach (Vector2Int size in _options.Resolutions)
            {
                sizes.Add($"{size.x} × {size.y}");
            }

            _resolution.ClearOptions();
            _resolution.AddOptions(sizes);

            _frameRateValues.Clear();
            List<string> caps = new List<string>();
            foreach (int cap in limits.FrameRateChoices)
            {
                _frameRateValues.Add(cap);
                caps.Add($"{cap} fps");
            }

            _frameRateValues.Add(0);
            caps.Add("Unlimited");
            _frameRateLimit.ClearOptions();
            _frameRateLimit.AddOptions(caps);

            _quality.ClearOptions();
            _quality.AddOptions(new List<string>(QualitySettings.names));
        }

        // Shows the options in effect without firing the controls' change events.
        private void Refresh()
        {
            GameOptions current = _options.Current;
            if (current == null || _frameRateValues.Count == 0)
            {
                return;
            }

            _sensitivity.SetValueWithoutNotify(current.MouseSensitivity);
            _sensitivityValue.text = current.MouseSensitivity.ToString("0.00", CultureInfo.InvariantCulture) + "×";
            _invertY.SetIsOnWithoutNotify(current.InvertY);
            _fieldOfView.SetValueWithoutNotify(current.FieldOfView);
            _fieldOfViewValue.text = Mathf.RoundToInt(current.FieldOfView) + "°";
            _displayMode.SetValueWithoutNotify((int)current.DisplayMode);

            IReadOnlyList<Vector2Int> sizes = _options.Resolutions;
            Resolution native = Screen.currentResolution;
            int width = current.HasResolution ? current.ResolutionWidth : native.width;
            int height = current.HasResolution ? current.ResolutionHeight : native.height;
            _resolution.SetValueWithoutNotify(Mathf.Max(0, ResolutionList.IndexOfClosest(sizes, width, height)));
            _resolution.interactable = sizes.Count > 0;

            _vSync.SetIsOnWithoutNotify(current.VSync);
            int capIndex = _frameRateValues.IndexOf(current.FrameRateLimit);
            _frameRateLimit.SetValueWithoutNotify(capIndex >= 0 ? capIndex : _frameRateValues.Count - 1);
            // VSync already caps the frame rate at the monitor's refresh rate.
            _frameRateLimit.interactable = !current.VSync;
            _quality.SetValueWithoutNotify(current.QualityLevel);
            _volume.SetValueWithoutNotify(current.MasterVolume);
            _volumeValue.text = Mathf.RoundToInt(current.MasterVolume * 100f) + "%";
        }

        private void OnResolutionChosen(int index)
        {
            IReadOnlyList<Vector2Int> sizes = _options.Resolutions;
            if (index < 0 || index >= sizes.Count)
            {
                return;
            }

            Vector2Int size = sizes[index];
            _options.Change(o =>
            {
                o.ResolutionWidth = size.x;
                o.ResolutionHeight = size.y;
            });
        }
    }
}
