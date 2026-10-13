namespace Cinderbird.Player
{
    /// <summary>Why a scoop was refused before digging.</summary>
    public enum ScoopRefusal
    {
        /// <summary>The last load hasn't been shaken out yet.</summary>
        AshInSieve,

        /// <summary>The sieve holds its ember capacity; pour them out first.</summary>
        SieveFullOfEmbers,
    }
}
