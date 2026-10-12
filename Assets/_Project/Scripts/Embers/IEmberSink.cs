using System.Collections.Generic;

namespace Cinderbird.Embers
{
    /// <summary>Something embers can be poured into: a basket or the nest.</summary>
    public interface IEmberSink
    {
        /// <summary>How many more embers fit; <see cref="int.MaxValue"/> for no limit.</summary>
        int FreeSpace { get; }

        /// <summary>Takes the embers; callers never pass more than <see cref="FreeSpace"/>.</summary>
        void Add(List<Ember> embers);
    }
}
