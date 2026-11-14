using UnityEngine;
using WoodcraftSimulator.Interaction;

namespace WoodcraftSimulator.Inventory
{
    /// <summary>
    /// A small item lying in the world that the player can take onto the tool belt with E.
    /// While stored, its colliders are off; the belt shows it in hand when selected.
    /// </summary>
    public sealed class PickupItem : MonoBehaviour, IInteractable, IDescribable
    {
        [SerializeField] private ItemDefinition definition;

        [Tooltip("Pose in the player's hand, relative to the camera (position in meters, rotation in degrees).")]
        [SerializeField] private Vector3 handPosition = new Vector3(0.22f, -0.2f, 0.45f);
        [SerializeField] private Vector3 handRotation = Vector3.zero;

        private Collider[] colliders;

        /// <summary>The domain item instance represented by this object. Null if the definition is missing.</summary>
        public InventoryItem Item { get; private set; }

        public ItemDefinition Definition => definition;

        public Vector3 HandPosition => handPosition;

        public Quaternion HandRotation => Quaternion.Euler(handRotation);

        /// <summary>Extra offset (camera space, meters) a tool can animate, e.g. saw strokes. Zero when idle.</summary>
        public Vector3 HandOffset { get; set; }

        public string Description => Item != null ? Item.DisplayName : name;

        public string InteractionPrompt => $"Take {Description}";

        private void Awake()
        {
            colliders = GetComponentsInChildren<Collider>(true);

            if (definition == null)
            {
                Debug.LogError($"{nameof(PickupItem)} '{name}': item definition is not assigned.", this);
                return;
            }

            Item = definition.CreateInstance();
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (Item != null && interactor.TryGetComponent(out PlayerToolBelt belt))
            {
                belt.TryStore(this);
            }
        }

        /// <summary>Called by <see cref="PlayerToolBelt"/>: stored items can't be hit or collided with.</summary>
        internal void SetStored(bool stored)
        {
            foreach (Collider itemCollider in colliders)
            {
                itemCollider.enabled = !stored;
            }
        }
    }
}
