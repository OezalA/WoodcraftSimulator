using UnityEngine;

namespace WoodcraftSimulator.Handling
{
    /// <summary>A pose offered by an <see cref="IPlacementTarget"/> for the carried object.</summary>
    public readonly struct SnapPlacement
    {
        public SnapPlacement(Vector3 position, Quaternion rotation, bool allowed, string message, int slotIndex)
        {
            Position = position;
            Rotation = rotation;
            Allowed = allowed;
            Message = message;
            SlotIndex = slotIndex;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        /// <summary>False: the pose is shown (red) with <see cref="Message"/> explaining why it can't be used.</summary>
        public bool Allowed { get; }

        /// <summary>Short text for the HUD (also for allowed snaps, e.g. "fits, 2.5 mm too long"), or null.</summary>
        public string Message { get; }

        /// <summary>Target-specific id of the slot (opaque to the carrier).</summary>
        public int SlotIndex { get; }
    }

    /// <summary>
    /// Something with predefined places (anchors) the carried object snaps into, e.g. an assembly station's slots.
    /// Found by <see cref="PlayerCarrier"/> on the aimed collider or its parents; ordinary surfaces don't need it.
    /// Keeps the handling code independent of what the target is (assembly, later racks with fixed places).
    /// </summary>
    public interface IPlacementTarget
    {
        string DisplayName { get; }

        /// <summary>Offers a snap pose when the aim point is close to one of the target's places.</summary>
        bool TryGetSnap(Carryable carried, Vector3 aimPoint, out SnapPlacement snap);

        /// <summary>Puts the (no longer carried) object into the snapped place.</summary>
        void Install(Carryable carryable, SnapPlacement snap);

        /// <summary>Whether an installed object may be taken out; otherwise a short reason for the HUD.</summary>
        bool CanRelease(Carryable installed, out string reason);

        /// <summary>Takes an installed object out (it is picked up right after).</summary>
        void Release(Carryable installed);
    }
}
