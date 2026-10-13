using System;
using System.Collections.Generic;

namespace Cinderbird.Options
{
    /// <summary>
    /// The allowed ranges and defaults for the options, as plain data so <see cref="GameOptions"/> can be tested
    /// without the <see cref="OptionsSettings"/> asset.
    /// </summary>
    public readonly struct OptionLimits
    {
        public OptionLimits(float minSensitivity, float maxSensitivity, float defaultSensitivity,
            float minFieldOfView, float maxFieldOfView, float defaultFieldOfView,
            int qualityLevelCount, int defaultQualityLevel, IReadOnlyList<int> frameRateChoices)
        {
            if (qualityLevelCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(qualityLevelCount), "There must be at least one quality level.");
            }

            MinSensitivity = minSensitivity;
            MaxSensitivity = Math.Max(minSensitivity, maxSensitivity);
            DefaultSensitivity = defaultSensitivity;
            MinFieldOfView = minFieldOfView;
            MaxFieldOfView = Math.Max(minFieldOfView, maxFieldOfView);
            DefaultFieldOfView = defaultFieldOfView;
            QualityLevelCount = qualityLevelCount;
            DefaultQualityLevel = Math.Min(Math.Max(0, defaultQualityLevel), qualityLevelCount - 1);
            FrameRateChoices = frameRateChoices ?? Array.Empty<int>();
        }

        public float MinSensitivity { get; }
        public float MaxSensitivity { get; }
        public float DefaultSensitivity { get; }
        public float MinFieldOfView { get; }
        public float MaxFieldOfView { get; }
        public float DefaultFieldOfView { get; }
        public int QualityLevelCount { get; }
        public int DefaultQualityLevel { get; }

        /// <summary>Frame-rate caps offered in the menu, besides "no limit" (0).</summary>
        public IReadOnlyList<int> FrameRateChoices { get; }
    }
}
