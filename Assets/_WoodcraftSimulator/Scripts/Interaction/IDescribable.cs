namespace WoodcraftSimulator.Interaction
{
    /// <summary>
    /// Optional short player-facing description of an object, e.g. "Spruce 2400 × 95 × 45 mm (4.6 kg)".
    /// Used by generic components (like Carryable) to build prompts without knowing the object type.
    /// </summary>
    public interface IDescribable
    {
        string Description { get; }
    }
}
