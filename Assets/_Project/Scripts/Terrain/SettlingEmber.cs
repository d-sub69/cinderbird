using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// An ember inside loose ash that may slide, as <see cref="AshSettler"/> tracks it: which column it's in and
    /// how much ash lies below it there, so it moves with the ash around it.
    /// </summary>
    public readonly struct SettlingEmber
    {
        public SettlingEmber(Ember ember, int chunk, int column, float ashBelow, Vector2 offset, bool needsPlacing)
        {
            Ember = ember;
            Chunk = chunk;
            Column = column;
            AshBelow = ashBelow;
            Offset = offset;
            NeedsPlacing = needsPlacing;
        }

        /// <summary>The ember as it's stored in the terrain right now.</summary>
        public Ember Ember { get; }

        /// <summary>The chunk whose list holds it; -1 once it's gone (dug out meanwhile).</summary>
        public int Chunk { get; }

        /// <summary>Index of its column in the settling area.</summary>
        public int Column { get; }

        /// <summary>Samples' worth of loose ash below it in its column.</summary>
        public float AshBelow { get; }

        /// <summary>Where in its column it sits, in cells from the column's center (x, z), kept when it moves.</summary>
        public Vector2 Offset { get; }

        /// <summary>
        /// Whether its stored position no longer matches its place in the ash (it slid, or a cave below it closed).
        /// Embers that didn't move keep their exact position.
        /// </summary>
        public bool NeedsPlacing { get; }

        public SettlingEmber MovedTo(int column, float ashBelow)
        {
            return new SettlingEmber(Ember, Chunk, column, ashBelow, Offset, true);
        }

        public SettlingEmber StoredAs(Ember ember, int chunk)
        {
            return new SettlingEmber(ember, chunk, Column, AshBelow, Offset, false);
        }
    }
}
