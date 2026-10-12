using System;
using System.Collections.Generic;
using Cinderbird.Embers;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// The rules for tipping the sieve into the basket.
    /// </summary>
    public static class EmberTransfer
    {
        /// <param name="buffer">Caller-owned scratch list, reused to avoid allocating per tip.</param>
        /// <param name="moved">How many embers moved; less than the sieve held if the basket filled up.</param>
        public static TipOutcome Tip(SieveContents sieve, BasketContents basket, List<Ember> buffer, out int moved)
        {
            moved = 0;

            // Ash check first: it's the most common mistake and the most useful message.
            if (sieve.AshVolume > 0f)
            {
                return TipOutcome.AshRemaining;
            }

            if (sieve.Embers.Count == 0)
            {
                return TipOutcome.NoEmbers;
            }

            if (basket.IsFull)
            {
                return TipOutcome.BasketFull;
            }

            int count = Math.Min(sieve.Embers.Count, basket.FreeSpace);
            buffer.Clear();
            moved = sieve.TakeNewestEmbers(count, buffer);
            basket.Add(buffer);
            buffer.Clear();
            return TipOutcome.Tipped;
        }
    }
}
