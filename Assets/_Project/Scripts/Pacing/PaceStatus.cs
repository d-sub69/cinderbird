namespace Cinderbird.Pacing
{
    /// <summary>Where the game stands at a moment, for a pacing line: the deepest open layer, Heat and Glow.</summary>
    public readonly struct PaceStatus
    {
        public PaceStatus(string layerName, float layerCleared, int heat, int heatLevel, int nextLevelHeat, int glow)
        {
            LayerName = layerName;
            LayerCleared = layerCleared;
            Heat = heat;
            HeatLevel = heatLevel;
            NextLevelHeat = nextLevelHeat;
            Glow = glow;
        }

        public string LayerName { get; }

        /// <summary>Share of that layer's embers taken out (0..1).</summary>
        public float LayerCleared { get; }

        public int Heat { get; }

        public int HeatLevel { get; }

        /// <summary>Heat the next level needs; 0 at the last level.</summary>
        public int NextLevelHeat { get; }

        public int Glow { get; }
    }
}
