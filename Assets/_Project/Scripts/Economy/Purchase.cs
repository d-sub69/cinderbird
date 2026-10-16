namespace Cinderbird.Economy
{
    /// <summary>How a try to buy an upgrade went.</summary>
    public enum Purchase
    {
        Bought,
        NotEnoughGlow,
        AlreadyMaxed,
        /// <summary>Not on sale yet: what it requires hasn't been bought.</summary>
        Locked,
    }
}
