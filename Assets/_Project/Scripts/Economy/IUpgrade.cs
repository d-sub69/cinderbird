namespace Cinderbird.Economy
{
    /// <summary>Something the shop sells in levels: an id that saves keep, a top level, and the price of each step.</summary>
    public interface IUpgrade
    {
        /// <summary>Stable id for saves; never change it once players have saves.</summary>
        string Id { get; }

        /// <summary>The highest level; level 0 is what the player starts with.</summary>
        int MaxLevel { get; }

        /// <summary>Glow to go from <paramref name="level"/> to the next.</summary>
        int PriceAt(int level);
    }
}
