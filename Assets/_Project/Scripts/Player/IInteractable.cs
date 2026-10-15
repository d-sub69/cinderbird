namespace Cinderbird.Player
{
    /// <summary>Something in the world the player can use by looking at it and pressing the use key (the shop stall).</summary>
    public interface IInteractable
    {
        /// <summary>What using it does, for the on-screen prompt (e.g. "open the shop").</summary>
        string Prompt { get; }

        void Interact();
    }
}
