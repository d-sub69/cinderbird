namespace Cinderbird.Embers
{
    /// <summary>Result of pouring a tool's embers into a target.</summary>
    public enum PourOutcome
    {
        /// <summary>At least one ember moved (maybe not all, if the target filled up).</summary>
        Poured,

        /// <summary>Refused: shake the ash out first, or it would go in with the embers.</summary>
        AshRemaining,

        /// <summary>Refused: nothing to pour.</summary>
        NoEmbers,

        /// <summary>Refused: the target has no room left.</summary>
        TargetFull,
    }
}
