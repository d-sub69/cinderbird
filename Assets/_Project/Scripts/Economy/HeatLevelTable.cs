using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>Heat needed for each nest Heat level (design: levels 1–10). Values are provisional, for tuning.</summary>
    [CreateAssetMenu(fileName = "HeatLevels", menuName = "Cinderbird/Heat Levels")]
    public sealed class HeatLevelTable : ScriptableObject
    {
        [Tooltip("Heat needed for level 1, 2, …, each more than the one before.")]
        [SerializeField] private int[] _thresholds = { 50, 250, 1000, 3000, 8000, 15000, 25000, 40000, 60000, 100000 };

        public HeatLevels CreateLevels()
        {
            return new HeatLevels(_thresholds);
        }
    }
}
