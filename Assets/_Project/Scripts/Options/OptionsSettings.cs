using UnityEngine;

namespace Cinderbird.Options
{
    /// <summary>Ranges, defaults and choices for the options menu.</summary>
    [CreateAssetMenu(fileName = "OptionsSettings", menuName = "Cinderbird/Options Settings")]
    public sealed class OptionsSettings : ScriptableObject
    {
        [SerializeField] private string _fileName = "options.json";

        [Header("Mouse (multiplier on the base sensitivity in the movement settings)")]
        [SerializeField, Min(0.01f)] private float _minSensitivity = 0.2f;
        [SerializeField, Min(0.01f)] private float _maxSensitivity = 3f;
        [SerializeField, Min(0.01f)] private float _defaultSensitivity = 1f;

        [Header("Field of view (degrees, vertical)")]
        [SerializeField, Range(30f, 120f)] private float _minFieldOfView = 50f;
        [SerializeField, Range(30f, 120f)] private float _maxFieldOfView = 100f;
        [SerializeField, Range(30f, 120f)] private float _defaultFieldOfView = 60f;

        [Header("Display")]
        [Tooltip("Index into Project Settings → Quality levels used on first start.")]
        [SerializeField, Min(0)] private int _defaultQualityLevel = 2;
        [Tooltip("Frame-rate caps offered while VSync is off, besides Unlimited.")]
        [SerializeField] private int[] _frameRateChoices = { 30, 60, 120, 144 };

        public string FileName => _fileName;

        public OptionLimits CreateLimits(int qualityLevelCount)
        {
            return new OptionLimits(_minSensitivity, _maxSensitivity, _defaultSensitivity,
                _minFieldOfView, _maxFieldOfView, _defaultFieldOfView,
                qualityLevelCount, _defaultQualityLevel, _frameRateChoices);
        }
    }
}
