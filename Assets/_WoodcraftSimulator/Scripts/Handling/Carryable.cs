using UnityEngine;
using WoodcraftSimulator.Interaction;

namespace WoodcraftSimulator.Handling
{
    /// <summary>
    /// Makes an object pick-up-able by the player (composition: add to any object with a BoxCollider).
    /// States: Resting (collider on, kinematic: stays exactly where it was placed) and
    /// Carried (colliders off so it neither blocks the player nor the placement checks).
    /// The BoxCollider defines the footprint used for placement validation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class Carryable : MonoBehaviour, IInteractable
    {
        private Rigidbody body;
        private BoxCollider footprint;
        private Collider[] colliders;
        private IDescribable describable;

        public bool IsCarried { get; private set; }

        /// <summary>Local-space size of the placement footprint (BoxCollider size).</summary>
        public Vector3 Size => footprint.size;

        /// <summary>Local-space center of the footprint relative to the pivot.</summary>
        public Vector3 Center => footprint.center;

        public string Description => describable != null ? describable.Description : name;

        public string InteractionPrompt => $"Pick up {Description}";

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            footprint = GetComponent<BoxCollider>();
            colliders = GetComponentsInChildren<Collider>(true);
            describable = GetComponent<IDescribable>();
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor.TryGetComponent(out PlayerCarrier carrier))
            {
                carrier.TryPickUp(this);
            }
        }

        /// <summary>Called by <see cref="PlayerCarrier"/> when the object is picked up or placed.</summary>
        internal void SetCarried(bool carried)
        {
            IsCarried = carried;

            // Kinematic in both states: carried objects are moved by code, placed objects stay put (D-019).
            body.isKinematic = true;
            // Interpolation would lag behind when the transform is set directly every frame while carried.
            body.interpolation = carried ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;

            foreach (Collider objectCollider in colliders)
            {
                objectCollider.enabled = !carried;
            }
        }
    }
}
