using UnityEngine;

namespace WoodcraftSimulator.Handling
{
    /// <summary>
    /// Optional on a <see cref="Carryable"/>: where it is held instead of the carrier's default (in front of the eyes).
    /// A tall object such as the stepladder is carried low at the side so it doesn't hide the view (D-050).
    /// </summary>
    public interface ICarryHold
    {
        /// <summary>Position of the object's pivot relative to the eyes (x right, y up, z forward), in meters.</summary>
        Vector3 HoldOffset { get; }
    }
}
