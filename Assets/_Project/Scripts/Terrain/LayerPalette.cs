using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// The ash and dust color of each ash layer, and the mix a dug load shows when it took ash from several.
    /// Without layer colors everything is the default grey ash (<see cref="AshColors"/>).
    /// </summary>
    public sealed class LayerPalette
    {
        private readonly Color[] _ashColors;
        private readonly Color[] _dustColors;

        /// <param name="ashColors">Per layer; null for the default everywhere.</param>
        /// <param name="dustColors">Per layer; null for the default everywhere.</param>
        public LayerPalette(Color[] ashColors, Color[] dustColors)
        {
            _ashColors = ashColors;
            _dustColors = dustColors;
        }

        /// <summary>The ash tints for the terrain mesh; null when there are none.</summary>
        public Color[] AshTints => _ashColors;

        /// <summary>The dust color of an ash layer, as the sieve shows it.</summary>
        public Color DustOf(int layer)
        {
            return _dustColors != null && layer >= 0 && layer < _dustColors.Length ? _dustColors[layer] : AshColors.DefaultDust;
        }

        /// <summary>The ash and dust colors of a dug load, blended by how much came from each layer.</summary>
        /// <param name="removedByLayer">Amount taken per layer; null gives the defaults.</param>
        public void Mix(float[] removedByLayer, out Color ashColor, out Color dustColor)
        {
            ashColor = _ashColors != null && removedByLayer != null
                ? LayerColorMix.Blend(removedByLayer, _ashColors, AshColors.DefaultAsh)
                : AshColors.DefaultAsh;
            dustColor = _dustColors != null && removedByLayer != null
                ? LayerColorMix.Blend(removedByLayer, _dustColors, AshColors.DefaultDust)
                : AshColors.DefaultDust;
        }
    }
}
