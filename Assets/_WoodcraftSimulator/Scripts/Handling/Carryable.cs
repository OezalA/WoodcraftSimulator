using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Interaction;

namespace WoodcraftSimulator.Handling
{
    /// <summary>
    /// Makes an object pick-up-able by the player (composition: add to any object with a BoxCollider).
    /// States: Resting (collider on, kinematic: stays exactly where it was placed), Carried (colliders off so it
    /// neither blocks the player nor the placement checks) and, briefly, Settling: when its support may have gone
    /// (cut off over an edge, the board below was taken away) and nothing is under its center of mass, physics
    /// takes over until it comes to rest, then it is kinematic again (D-031).
    /// The BoxCollider defines the footprint used for placement validation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class Carryable : MonoBehaviour, IInteractable
    {
        // Settling: at rest when slower than these for RestTime, or forced after MaxSettleTime.
        private const float RestLinearSpeed = 0.02f;
        private const float RestAngularSpeed = 0.1f;
        private const float RestTime = 0.3f;
        private const float MaxSettleTime = 6f;
        private const float SupportProbeExtra = 0.01f;
        private const float NeighbourMargin = 0.01f;

        private static readonly RaycastHit[] ProbeHits = new RaycastHit[8];
        private static readonly Collider[] NeighbourBuffer = new Collider[32];

        private Rigidbody body;
        private BoxCollider footprint;
        private Collider[] colliders;
        private IDescribable describable;

        private bool supportCheckPending;
        private bool settling;
        private float settleTime;
        private float restTime;

        public bool IsCarried { get; private set; }

        /// <summary>Local-space size of the placement footprint (BoxCollider size).</summary>
        public Vector3 Size => footprint.size;

        /// <summary>Local-space center of the footprint relative to the pivot.</summary>
        public Vector3 Center => footprint.center;

        public string Description => describable != null ? describable.Description : name;

        /// <summary>The target this object is installed in (e.g. an assembly slot), or null.</summary>
        public IPlacementTarget InstalledIn { get; private set; }

        public string InteractionPrompt
        {
            get
            {
                if (InstalledIn == null)
                {
                    return Localization.Loc.T($"Pick up {Description}", $"{Description} al");
                }

                return InstalledIn.CanRelease(this, out string reason)
                    ? Localization.Loc.T($"Take {Description} out of {InstalledIn.DisplayName}",
                        $"{Description} parçasını {InstalledIn.DisplayName} içinden çıkar")
                    : reason;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            footprint = GetComponent<BoxCollider>();
            colliders = GetComponentsInChildren<Collider>(true);
            describable = GetComponent<IDescribable>();
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!interactor.TryGetComponent(out PlayerCarrier carrier) || carrier.IsCarrying)
            {
                return;
            }

            if (InstalledIn != null)
            {
                if (!InstalledIn.CanRelease(this, out _))
                {
                    return;
                }

                InstalledIn.Release(this);
            }

            carrier.TryPickUp(this);
        }

        /// <summary>Called by an <see cref="IPlacementTarget"/>: installed (null = free again). Stays kinematic.</summary>
        public void SetInstalled(IPlacementTarget target)
        {
            InstalledIn = target;
            supportCheckPending = false;
            settling = false;
            body.isKinematic = true;
        }

        /// <summary>Called by <see cref="PlayerCarrier"/> when the object is picked up or placed.</summary>
        internal void SetCarried(bool carried)
        {
            IsCarried = carried;
            supportCheckPending = false;
            settling = false;

            // Kinematic in both states: carried objects are moved by code, placed objects stay put (D-019).
            body.isKinematic = true;
            // Interpolation would lag behind when the transform is set directly every frame while carried.
            body.interpolation = carried ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;

            foreach (Collider objectCollider in colliders)
            {
                objectCollider.enabled = !carried;
            }
        }

        /// <summary>
        /// Asks this resting object to check, at the next physics step, whether something is still under its
        /// center of mass; if not, it falls and settles. Use after its support may have changed.
        /// </summary>
        public void RequestSupportCheck()
        {
            if (!IsCarried && InstalledIn == null)
            {
                supportCheckPending = true;
            }
        }

        /// <summary>
        /// Asks every other resting carryable touching this one from above to check its support, e.g. before this
        /// one is picked up from under them.
        /// </summary>
        public void RequestSupportCheckAbove() => ForEachCarryableAbove(other => other.RequestSupportCheck());

        private void ForEachCarryableAbove(System.Action<Carryable> action)
        {
            Vector3 worldCenter = transform.TransformPoint(footprint.center);
            Vector3 halfExtents = footprint.size * 0.5f + Vector3.one * NeighbourMargin;
            int count = Physics.OverlapBoxNonAlloc(worldCenter, halfExtents, NeighbourBuffer, transform.rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            var seen = new HashSet<Carryable>();
            for (int i = 0; i < count; i++)
            {
                Carryable other = NeighbourBuffer[i].GetComponentInParent<Carryable>();
                // "Above" = its lowest point is at or above this one's top (neighbours side by side don't count).
                if (other != null && other != this && seen.Add(other) &&
                    other.LowestPointY() >= worldCenter.y + WorldHalfHeight() - NeighbourMargin)
                {
                    action(other);
                }
            }
        }

        private void FixedUpdate()
        {
            if (supportCheckPending)
            {
                supportCheckPending = false;
                if (!IsCarried && !settling && !HasSupportBelowCenterOfMass())
                {
                    StartSettling();
                }
            }

            if (settling)
            {
                UpdateSettling();
            }
        }

        private bool HasSupportBelowCenterOfMass()
        {
            // Probe straight down from the center of mass (the box center for uniform timber), a bit beyond the
            // lowest point of the footprint. Physics decides the rest (tipping over an edge, resting on two supports).
            Vector3 origin = transform.TransformPoint(footprint.center);
            float halfHeight = WorldHalfHeight();

            int count = Physics.RaycastNonAlloc(origin, Vector3.down, ProbeHits, halfHeight + SupportProbeExtra,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!ProbeHits[i].collider.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        private void ForceSettling()
        {
            if (!IsCarried && !settling && InstalledIn == null)
            {
                StartSettling();
            }
        }

        /// <summary>Vertical half extent of the (rotated) footprint in world space.</summary>
        private float WorldHalfHeight()
        {
            Vector3 halfExtents = footprint.size * 0.5f;
            return Mathf.Abs(transform.right.y * halfExtents.x) + Mathf.Abs(transform.up.y * halfExtents.y) +
                   Mathf.Abs(transform.forward.y * halfExtents.z);
        }

        private float LowestPointY() => transform.TransformPoint(footprint.center).y - WorldHalfHeight();

        private void StartSettling()
        {
            settling = true;
            settleTime = 0f;
            restTime = 0f;
            body.isKinematic = false;
            body.WakeUp();

            // Whatever lies on this object loses its support with it: let those fall along (physics, not a probe that
            // would still find this object under them in the next step).
            ForEachCarryableAbove(other => other.ForceSettling());
        }

        private void UpdateSettling()
        {
            settleTime += Time.fixedDeltaTime;
            bool atRest = body.linearVelocity.magnitude < RestLinearSpeed &&
                          body.angularVelocity.magnitude < RestAngularSpeed;
            restTime = atRest ? restTime + Time.fixedDeltaTime : 0f;

            if (restTime >= RestTime || body.IsSleeping() || settleTime >= MaxSettleTime)
            {
                settling = false;
                body.isKinematic = true;
            }
        }
    }
}
