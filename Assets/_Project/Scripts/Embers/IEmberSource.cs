using System.Collections.Generic;

namespace Cinderbird.Embers
{
    /// <summary>Something embers can be poured out of: the sieve or the basket.</summary>
    public interface IEmberSource
    {
        int EmberCount { get; }

        /// <summary>Loose ash that has to be shaken out before the embers can be poured.</summary>
        bool HasAsh { get; }

        /// <summary>Moves up to <paramref name="count"/> of the most recently added embers into <paramref name="output"/>.</summary>
        /// <returns>How many were taken.</returns>
        int TakeNewest(int count, List<Ember> output);
    }
}
