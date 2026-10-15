using UnityEngine;

namespace Cinderbird.Offerings
{
    /// <summary>Offerings that belong together: all of them on the shrine shelf gives a lasting bonus.</summary>
    [CreateAssetMenu(fileName = "OfferingSet", menuName = "Cinderbird/Offerings/Set")]
    public sealed class OfferingSet : ScriptableObject
    {
        [Tooltip("Stable id; also names its bonus.")]
        [SerializeField] private string _id = "set";
        [SerializeField] private string _displayName = "Set";
        [SerializeField] private OfferingDefinition[] _members = new OfferingDefinition[0];
        [SerializeField] private BonusKind _bonus = BonusKind.Glow;
        [Tooltip("Extra Glow or Heat from feeding, in percent, while the whole set is on the shelf.")]
        [SerializeField, Min(0f)] private float _percent = 10f;

        public string Id => _id;
        public string DisplayName => _displayName;
        public OfferingDefinition[] Members => _members;
        public BonusKind Bonus => _bonus;
        public float Percent => _percent;

        public string DescribeBonus()
        {
            return $"+{_percent:0}% {(_bonus == BonusKind.Glow ? "Glow" : "Heat")} from feeding the nest";
        }
    }
}
