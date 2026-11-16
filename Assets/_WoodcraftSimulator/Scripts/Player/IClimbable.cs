using UnityEngine;

namespace WoodcraftSimulator.Player
{
    /// <summary>Something the player can stand on to work higher up, e.g. a stepladder (D-049).</summary>
    public interface IClimbable
    {
        /// <summary>Whether it can be climbed now (e.g. standing upright, not being carried); otherwise why not.</summary>
        bool CanClimb(out string reason);

        /// <summary>World position of the player's feet while standing on it.</summary>
        Vector3 StandPosition { get; }

        /// <summary>Called when the player gets on (true) or off (false).</summary>
        void SetOccupied(bool occupied);
    }
}
