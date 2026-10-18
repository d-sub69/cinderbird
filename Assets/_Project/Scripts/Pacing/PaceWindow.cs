namespace Cinderbird.Pacing
{
    /// <summary>
    /// What happened over a stretch of play (a minute, or the whole game so far): ash moved and embers dug by source,
    /// embers fed, Glow earned and spent, purchases, and how quickly the player's scoops followed each other.
    /// </summary>
    public sealed class PaceWindow
    {
        private const int SourceCount = 3;

        private readonly float[] _volume = new float[SourceCount];
        private readonly int[] _embers = new int[SourceCount];
        private readonly int[] _scoops = new int[SourceCount];

        /// <summary>Seconds of play it covers.</summary>
        public double Seconds { get; set; }

        public int EmbersFed { get; private set; }

        public int Feeds { get; private set; }

        public long GlowEarned { get; private set; }

        public long GlowSpent { get; private set; }

        public int Purchases { get; private set; }

        /// <summary>Gaps between the player's scoops short enough to count as sifting in a row, and their total.</summary>
        public int SteadyGaps { get; private set; }

        public double SteadyGapSeconds { get; private set; }

        public float Volume(PaceSource source) => _volume[(int)source];

        public int Embers(PaceSource source) => _embers[(int)source];

        public int Scoops(PaceSource source) => _scoops[(int)source];

        public float TotalVolume => _volume[0] + _volume[1] + _volume[2];

        public int TotalEmbers => _embers[0] + _embers[1] + _embers[2];

        /// <summary>Average seconds between the player's scoops while sifting in a row; 0 without any.</summary>
        public double SecondsPerScoop => SteadyGaps > 0 ? SteadyGapSeconds / SteadyGaps : 0.0;

        /// <summary>Average liters per scoop of the player's sieve; 0 without any.</summary>
        public float LitersPerScoop(PaceSource source)
        {
            int scoops = Scoops(source);
            return scoops > 0 ? Volume(source) * 1000f / scoops : 0f;
        }

        public void AddDig(PaceSource source, float volume, int embers)
        {
            int index = (int)source;
            _volume[index] += volume > 0f ? volume : 0f;
            _embers[index] += embers > 0 ? embers : 0;
            _scoops[index]++;
        }

        public void AddSteadyGap(double seconds)
        {
            SteadyGaps++;
            SteadyGapSeconds += seconds;
        }

        public void AddFeed(int embers)
        {
            Feeds++;
            EmbersFed += embers;
        }

        public void AddGlow(long delta)
        {
            if (delta > 0)
            {
                GlowEarned += delta;
            }
            else
            {
                GlowSpent -= delta;
            }
        }

        public void AddPurchase()
        {
            Purchases++;
        }

        public PaceWindow Copy()
        {
            PaceWindow copy = new PaceWindow();
            copy.AddFrom(this);
            return copy;
        }

        public void AddFrom(PaceWindow other)
        {
            for (int i = 0; i < SourceCount; i++)
            {
                _volume[i] += other._volume[i];
                _embers[i] += other._embers[i];
                _scoops[i] += other._scoops[i];
            }

            Seconds += other.Seconds;
            EmbersFed += other.EmbersFed;
            Feeds += other.Feeds;
            GlowEarned += other.GlowEarned;
            GlowSpent += other.GlowSpent;
            Purchases += other.Purchases;
            SteadyGaps += other.SteadyGaps;
            SteadyGapSeconds += other.SteadyGapSeconds;
        }

        public void Clear()
        {
            for (int i = 0; i < SourceCount; i++)
            {
                _volume[i] = 0f;
                _embers[i] = 0;
                _scoops[i] = 0;
            }

            Seconds = 0.0;
            EmbersFed = 0;
            Feeds = 0;
            GlowEarned = 0;
            GlowSpent = 0;
            Purchases = 0;
            SteadyGaps = 0;
            SteadyGapSeconds = 0.0;
        }
    }
}
