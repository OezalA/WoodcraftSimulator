namespace WoodcraftSimulator.Interaction
{
    /// <summary>
    /// Optional second line under an object's interaction prompt, for an action on another key (e.g. "[Space] Climb the
    /// stepladder" under "[E] Pick up stepladder"). Implemented on the same GameObject as the interactable.
    /// </summary>
    public interface IInteractionHint
    {
        /// <summary>The extra line (with its key), or null when there is nothing to add right now.</summary>
        string InteractionHint { get; }
    }
}
