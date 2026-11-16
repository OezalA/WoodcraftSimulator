namespace WoodcraftSimulator.Handling
{
    /// <summary>
    /// Optional veto on picking up a <see cref="Carryable"/> (same GameObject), e.g. a stepladder someone stands on.
    /// </summary>
    public interface ICarryCondition
    {
        bool CanPickUp(out string reason);
    }
}
