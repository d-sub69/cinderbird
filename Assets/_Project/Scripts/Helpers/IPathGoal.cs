namespace Cinderbird.Helpers
{
    /// <summary>Where a path search may end: any cell that passes, so "anywhere within reach of the ash" is one search.</summary>
    public interface IPathGoal
    {
        bool IsGoal(int x, int z);

        /// <summary>A lower bound of the remaining cost in cells (0 when unsure), which steers the search toward the goal.</summary>
        float Estimate(int x, int z);
    }
}
