using System.Globalization;
using UnityEngine;

namespace Cinderbird.Economy
{
    /// <summary>The sieve's mesh: finer levels let fewer small embers slip through while shaking.</summary>
    [CreateAssetMenu(fileName = "Upgrade_Mesh", menuName = "Cinderbird/Upgrades/Mesh")]
    public sealed class MeshUpgrade : UpgradeDefinition
    {
        [Tooltip("Level 0 first: the mesh the player starts with.")]
        [SerializeField] private MeshLevel[] _levels =
        {
            new MeshLevel("Coarse mesh", 0.3f),
        };

        public float SlipChanceAt(int level)
        {
            return _levels[Clamp(level, _levels.Length)].SlipChance;
        }

        public override string LevelName(int level)
        {
            return _levels[Clamp(level, _levels.Length)].Name;
        }

        public override string Describe(int level)
        {
            float chance = SlipChanceAt(level);
            return chance <= 0f
                ? "Catches every spark"
                : string.Format(CultureInfo.InvariantCulture, "{0:0}% of sparks slip through", chance * 100f);
        }

        protected override int LevelCount => _levels.Length;
    }
}
