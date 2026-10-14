using System;
using System.Text;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Everything that decides how the fresh mountain is generated, as text. A save only stores changed chunks, so
    /// it can only be applied to a mountain with the same fingerprint. Ember spawn settings are left out on purpose:
    /// retuning ember numbers keeps saves, which simply keep the embers they stored.
    /// </summary>
    public static class TerrainFingerprint
    {
        // Invariant culture: the same mountain must give the same text on every PC, whatever its number format.
        /// <param name="layers">Null for a one-layer mountain.</param>
        public static string Build(VoxelTerrainSettings settings, LayerCatalog layers)
        {
            AshPileShape shape = settings.PileShape;
            StringBuilder builder = new StringBuilder();
            builder.Append(FormattableString.Invariant(
                $"world {settings.ChunkCells} {settings.ChunksX}x{settings.ChunksY}x{settings.ChunksZ} {settings.CellSize:R}; "));
            builder.Append(FormattableString.Invariant(
                $"pile {shape.Radius:R} {shape.Height:R} {shape.NoiseAmplitude:R} {shape.NoiseScale:R} {shape.NoiseSeed}"));
            if (layers == null)
            {
                return builder.ToString();
            }

            builder.Append("; layers");
            for (int i = 0; i < layers.Count; i++)
            {
                float start = layers[i] != null ? layers[i].StartDepth : 0f;
                builder.Append(FormattableString.Invariant($" {start:R}"));
            }

            builder.Append(FormattableString.Invariant($"; pocket {layers.PocketCenter.y:R} {layers.PocketRadius:R}"));
            return builder.ToString();
        }
    }
}
