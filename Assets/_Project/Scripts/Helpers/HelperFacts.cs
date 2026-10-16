namespace Cinderbird.Helpers
{
    /// <summary>What a helper knows about herself right now; the input to <see cref="HelperBrain"/>.</summary>
    public readonly struct HelperFacts
    {
        public HelperFacts(bool talking, bool busy, bool sieveHasAsh, bool sieveFull, bool sieveHasEmbers, bool hasWork,
            bool atWork, bool atNest, bool atHome, bool spotPending = false)
        {
            Talking = talking;
            Busy = busy;
            SieveHasAsh = sieveHasAsh;
            SieveFull = sieveFull;
            SieveHasEmbers = sieveHasEmbers;
            HasWork = hasWork;
            AtWork = atWork;
            AtNest = atNest;
            AtHome = atHome;
            SpotPending = spotPending;
        }

        /// <summary>The player is talking to her (or choosing where to send her).</summary>
        public bool Talking { get; }

        /// <summary>A scoop is still pouring into her sieve, or a pour is still tipping.</summary>
        public bool Busy { get; }

        public bool SieveHasAsh { get; }

        /// <summary>Her sieve holds as many embers as it can.</summary>
        public bool SieveFull { get; }

        public bool SieveHasEmbers { get; }

        /// <summary>She has a spot to dig (in her mode's plot, or the next nearest).</summary>
        public bool HasWork { get; }

        /// <summary>She stands where she can scoop that spot.</summary>
        public bool AtWork { get; }

        /// <summary>She stands where she can pour into the nest.</summary>
        public bool AtNest { get; }

        /// <summary>She stands by the shop.</summary>
        public bool AtHome { get; }

        /// <summary>The player sent her to a spot she hasn't reached yet: she goes there before anything else.</summary>
        public bool SpotPending { get; }
    }
}
