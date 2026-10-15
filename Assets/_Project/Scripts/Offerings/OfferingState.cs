namespace Cinderbird.Offerings
{
    /// <summary>
    /// Where an offering is in its life: in the ash, uncovered and lying on the pile, in the shrine's collection, on the
    /// shrine shelf, or sold. Saved as numbers: never reorder.
    /// </summary>
    public enum OfferingState
    {
        Buried = 0,
        InCollection = 1,
        OnShelf = 2,
        Sold = 3,
        OnPile = 4,
    }
}
