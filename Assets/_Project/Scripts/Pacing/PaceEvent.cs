namespace Cinderbird.Pacing
{
    /// <summary>A moment in a simulated run: a Heat level, a layer opening, a purchase.</summary>
    public readonly struct PaceEvent
    {
        public PaceEvent(double seconds, string text)
        {
            Seconds = seconds;
            Text = text;
        }

        public double Seconds { get; }

        public string Text { get; }
    }
}
