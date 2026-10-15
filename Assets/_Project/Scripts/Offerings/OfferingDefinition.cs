using UnityEngine;

namespace Cinderbird.Offerings
{
    /// <summary>
    /// One offering villagers left at the shrine: its name, one line of lore, what it sells for, where it's buried and what it
    /// looks like.
    /// </summary>
    [CreateAssetMenu(fileName = "Offering", menuName = "Cinderbird/Offerings/Offering")]
    public sealed class OfferingDefinition : ScriptableObject
    {
        [Tooltip("Stable id used by saves. Never change it once players have saves.")]
        [SerializeField] private string _id = "offering";
        [SerializeField] private string _displayName = "Offering";
        [SerializeField, TextArea(2, 4)] private string _lore = "";
        [Tooltip("Glow it sells for (and costs to buy back).")]
        [SerializeField, Min(0)] private int _sellValue = 30;
        [Tooltip("Color of its placeholder on the shrine shelf.")]
        [SerializeField] private Color _color = new Color(0.8f, 0.65f, 0.4f);
        [SerializeField] private OfferingPlacement _placement = new OfferingPlacement(0f, 0.8f, 0.5f);
        [Tooltip("What it looks like: shown lying on the pile and in the inspect view. Should have a collider so it can be aimed at.")]
        [SerializeField] private GameObject _model;
        [Tooltip("About how big it is across, in meters: how it rests on the ash and how it is framed when inspected.")]
        [SerializeField, Min(0.02f)] private float _size = 0.15f;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Lore => _lore;
        public int SellValue => _sellValue;
        public Color Color => _color;
        public OfferingPlacement Placement => _placement;
        public GameObject Model => _model;
        public float Size => _size;
    }
}
