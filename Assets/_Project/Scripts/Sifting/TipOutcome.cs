namespace Cinderbird.Sifting
{
    /// <summary>Result of trying to tip the sieve into the basket.</summary>
    public enum TipOutcome
    {
        /// <summary>At least one ember moved (maybe not all, if the basket filled up).</summary>
        Tipped,

        /// <summary>Refused: shake the ash out first, or it would end up in the basket.</summary>
        AshRemaining,

        /// <summary>Refused: nothing in the sieve to tip.</summary>
        NoEmbers,

        /// <summary>Refused: the basket has no room left.</summary>
        BasketFull,
    }
}
