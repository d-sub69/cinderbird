using System;
using System.Collections.Generic;

namespace Cinderbird.Embers
{
    /// <summary>
    /// The one set of pouring rules for every tool and target: sieve → basket, sieve → nest, basket → nest.
    /// </summary>
    public static class EmberTransfer
    {
        /// <param name="moved">Caller-owned list, cleared and then filled with the embers that moved (for visuals).</param>
        public static PourOutcome Pour(IEmberSource source, IEmberSink sink, List<Ember> moved)
        {
            moved.Clear();

            // Ash first: it's the most common mistake and the most useful message.
            if (source.HasAsh)
            {
                return PourOutcome.AshRemaining;
            }

            if (source.EmberCount == 0)
            {
                return PourOutcome.NoEmbers;
            }

            if (sink.FreeSpace <= 0)
            {
                return PourOutcome.TargetFull;
            }

            // Newest first, so embers left behind keep their spots in the source's display.
            int count = Math.Min(source.EmberCount, sink.FreeSpace);
            source.TakeNewest(count, moved);
            sink.Add(moved);
            return PourOutcome.Poured;
        }
    }
}
