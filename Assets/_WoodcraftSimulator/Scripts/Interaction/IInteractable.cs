namespace WoodcraftSimulator.Interaction
{
    /// <summary>
    /// Implemented by components the player can interact with by looking at them and pressing Interact.
    /// The component must live on the GameObject that owns the hit collider, or on one of its parents.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Short action phrase shown in the HUD, e.g. "Turn on lamp".
        /// Read when the object gains focus and again after each interaction.
        /// </summary>
        string InteractionPrompt { get; }

        void Interact(PlayerInteractor interactor);
    }
}
