namespace Cinderbird.Terrain
{
    /// <summary>The sample columns a settle loads and may change, inclusive on both ends.</summary>
    public readonly struct SettleArea
    {
        public SettleArea(int minX, int minZ, int maxX, int maxZ)
        {
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }

        /// <summary>
        /// Whether two areas share a column or touch: then settling one would read or write ash the other also works on.
        /// Areas that keep a column apart can settle side by side.
        /// </summary>
        public bool Touches(SettleArea other)
        {
            return MinX <= other.MaxX + 1 && other.MinX <= MaxX + 1 && MinZ <= other.MaxZ + 1 && other.MinZ <= MaxZ + 1;
        }
    }
}
