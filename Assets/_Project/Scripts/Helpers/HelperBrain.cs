namespace Cinderbird.Helpers
{
    /// <summary>
    /// A helper's priorities, as plain rules: listen when spoken to, finish what is playing, shake out ash before anything
    /// else, take a full sieve to the nest, go to the spot the player sent her to, take what she carries to the nest once
    /// there is nothing left to do, then dig, and otherwise wait by the shop. Checked every frame; each answer is one step of her day.
    /// </summary>
    public static class HelperBrain
    {
        public static HelperActivity Decide(HelperMode mode, HelperFacts facts)
        {
            if (facts.Talking || facts.Busy)
            {
                return HelperActivity.Wait;
            }

            // A load is shaken out wherever she is: carrying ash around would only spill it.
            if (facts.SieveHasAsh)
            {
                return HelperActivity.Shake;
            }

            if (facts.SieveFull)
            {
                return facts.AtNest ? HelperActivity.Pour : HelperActivity.GoToNest;
            }

            // Sent to a spot: she goes there first, always, and starts from there.
            if (facts.SpotPending)
            {
                return HelperActivity.GoToSpot;
            }

            bool stopping = mode == HelperMode.Off || !facts.HasWork;
            if (stopping && facts.SieveHasEmbers)
            {
                return facts.AtNest ? HelperActivity.Pour : HelperActivity.GoToNest;
            }

            if (stopping)
            {
                return facts.AtHome ? HelperActivity.Idle : HelperActivity.GoHome;
            }

            return facts.AtWork ? HelperActivity.Scoop : HelperActivity.GoToWork;
        }
    }
}
