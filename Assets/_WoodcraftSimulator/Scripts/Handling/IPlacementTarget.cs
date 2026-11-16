using System.Collections.Generic;
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

        /// <summary>
        /// Offers a snap pose when the view ray passes through one of the target's empty places (its ghost), even with
        /// nothing solid behind it, e.g. a beam slot in the air on top of the posts (D-050).
        /// <paramref name="distance"/> is where the ray enters that place.
        /// </summary>
        bool TryGetSnapOnRay(Carryable carried, Ray ray, float maxDistance, out SnapPlacement snap, out float distance);

        /// <summary>Puts the (no longer carried) object into the snapped place.</summary>
        void Install(Carryable carryable, SnapPlacement snap);

        /// <summary>Whether an installed object may be taken out; otherwise a short reason for the HUD.</summary>
        bool CanRelease(Carryable installed, out string reason);

        /// <summary>Takes an installed object out (it is picked up right after).</summary>
        void Release(Carryable installed);
    }

    /// <summary>
    /// The active placement targets (each registers itself while enabled), so the carrier can ask them about their
    /// empty places along the view ray, which have no colliders to hit (D-050).
    /// </summary>
    public static class PlacementTargets
    {
        private static readonly List<IPlacementTarget> Active = new List<IPlacementTarget>();

        public static IReadOnlyList<IPlacementTarget> All => Active;

        public static void Register(IPlacementTarget target)
        {
            if (target != null && !Active.Contains(target))
            {
                Active.Add(target);
            }
        }

        public static void Unregister(IPlacementTarget target) => Active.Remove(target);
    }
}
