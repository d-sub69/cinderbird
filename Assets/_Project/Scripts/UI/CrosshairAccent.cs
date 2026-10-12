using UnityEngine;

namespace Cinderbird.UI
{
    /// <summary>
    /// Put on an interactable (the nest; later the shop, the shrine) to give the center dot its own color,
    /// and optionally a pulsing glow, while the player looks at it.
    /// </summary>
    public sealed class CrosshairAccent : MonoBehaviour
    {
        [SerializeField] private Color _color = new Color(1f, 0.55f, 0.15f, 1f);
        [Tooltip("Show a soft pulsing halo around the dot.")]
        [SerializeField] private bool _glow = true;

        public Color Color => _color;
        public bool Glow => _glow;
    }
}
