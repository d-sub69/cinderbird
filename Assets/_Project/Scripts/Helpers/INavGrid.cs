namespace Cinderbird.Helpers
{
    /// <summary>A grid of cells a walker can move across, as <see cref="GridPathfinder"/> sees it.</summary>
    public interface INavGrid
    {
        int Width { get; }

        int Depth { get; }

        /// <summary>Whether a walker can stand in the cell.</summary>
        bool IsWalkable(int x, int z);

        /// <summary>Whether a walker can go straight from one cell to a neighboring one (free, and not too steep).</summary>
        bool CanStep(int fromX, int fromZ, int toX, int toZ);

        /// <summary>Height of the walkable surface in the cell, for the climb cost.</summary>
        float HeightAt(int x, int z);
    }
}
