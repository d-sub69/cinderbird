namespace Cinderbird.Terrain
{
    /// <summary>A stretch of one ash layer within a column of loose ash, measured in samples' worth of ash.</summary>
    public readonly struct LayerRun
    {
        public LayerRun(byte layer, float amount)
        {
            Layer = layer;
            Amount = amount;
        }

        public byte Layer { get; }

        public float Amount { get; }

        public LayerRun WithAmount(float amount)
        {
            return new LayerRun(Layer, amount);
        }
    }
}
