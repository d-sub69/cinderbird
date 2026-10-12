namespace Cinderbird.Nest
{
    /// <summary>What a batch of embers is worth, or what one feeding earned.</summary>
    public readonly struct FeedResult
    {
        public FeedResult(int emberCount, int glow, int heat)
        {
            EmberCount = emberCount;
            Glow = glow;
            Heat = heat;
        }

        public int EmberCount { get; }

        public int Glow { get; }

        public int Heat { get; }
    }
}
