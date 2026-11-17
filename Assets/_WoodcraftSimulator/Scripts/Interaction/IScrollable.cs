namespace WoodcraftSimulator.Interaction
{
    /// <summary>
    /// An interactable that uses the mouse wheel (Player/AdjustTool) while it is looked at, e.g. choosing a piece in an
    /// offcut bin (D-052). <see cref="PlayerInteractor"/> passes the wheel on; the tool in hand ignores it meanwhile
    /// (<see cref="WheelFocus"/>), so looking at a bin doesn't also turn the drill's clutch.
    /// </summary>
    public interface IScrollable : IInteractable
    {
        /// <param name="steps">+1 per notch up, -1 per notch down.</param>
        void Scroll(int steps);
    }

    /// <summary>Whether the wheel currently belongs to the looked-at object instead of the tool in hand.</summary>
    public static class WheelFocus
    {
        public static bool HeldByTarget { get; internal set; }
    }
}
