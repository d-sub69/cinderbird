namespace Cinderbird.Embers
{
    /// <summary>A component on a tool that carries embers (sieve, basket), exposing them for pouring.</summary>
    public interface IEmberHolder
    {
        IEmberSource Source { get; }
    }
}
