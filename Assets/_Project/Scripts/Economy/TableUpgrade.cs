using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>
    /// An upgrade whose levels are a table: each level lists its own price, value and shop text, for upgrades whose
    /// prices don't follow one curve (hiring a helper is a different purchase from making her faster). Whoever uses it
    /// reads <see cref="ValueAt"/>; what the value means is up to them (e.g. 1 = hired, an animation-time factor, a capacity).
    /// </summary>
    [CreateAssetMenu(fileName = "Upgrade_Table", menuName = "Cinderbird/Upgrades/Table")]
    public sealed class TableUpgrade : UpgradeDefinition
    {
        [Tooltip("Level 0 first: what the player starts with.")]
        [SerializeField] private TableLevel[] _levels =
        {
            new TableLevel("None", 0, 0f, "Nothing yet"),
        };

        public float ValueAt(int level)
        {
            return _levels[Clamp(level, _levels.Length)].Value;
        }

        public override int PriceAt(int level)
        {
            return _levels[Clamp(level + 1, _levels.Length)].Price;
        }

        public override string LevelName(int level)
        {
            return _levels[Clamp(level, _levels.Length)].Name;
        }

        public override string Describe(int level)
        {
            return _levels[Clamp(level, _levels.Length)].Description;
        }

        protected override int LevelCount => _levels.Length;
    }
}
