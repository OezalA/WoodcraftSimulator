using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Handling;

namespace WoodcraftSimulator.Inventory
{
    /// <summary>
    /// Scene side of the player's <see cref="ToolBelt"/>: stores <see cref="PickupItem"/> objects, selects them
    /// with the ToolSlot1..N keys, and shows the selected one in hand.
    /// Hands-full rule: picking up a large object holsters the tool, and slots can't be switched while carrying.
    /// </summary>
    public sealed class PlayerToolBelt : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlayerCarrier carrier;
        [SerializeField, Min(1)] private int slotCount = 4;

        private InputAction[] slotActions;
        private PickupItem[] storedObjects;

        /// <summary>Domain state (contents and selection). Created in Awake.</summary>
        public ToolBelt Belt { get; private set; }

        /// <summary>The scene object of the item in hand, or null.</summary>
        public PickupItem ItemInHand =>
            Belt.SelectedSlot == ToolBelt.NoSelection ? null : storedObjects[Belt.SelectedSlot];

        /// <summary>Raised after contents or selection changed (forwarded from the domain belt).</summary>
        public event Action Changed;

        private void Awake()
        {
            if (cameraTransform == null || carrier == null)
            {
                Debug.LogError($"{nameof(PlayerToolBelt)}: Camera Transform and Carrier must be assigned.", this);
                enabled = false;
                return;
            }

            Belt = new ToolBelt(slotCount);
            storedObjects = new PickupItem[slotCount];
            Belt.Changed += OnBeltChanged;

            slotActions = new InputAction[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                // Missing actions (more slots than bound keys) are tolerated: that slot just has no hotkey.
                slotActions[i] = InputSystem.actions.FindAction($"Player/ToolSlot{i + 1}");
            }

            carrier.CarryingChanged += OnCarryingChanged;
        }

        private void OnDestroy()
        {
            if (carrier != null)
            {
                carrier.CarryingChanged -= OnCarryingChanged;
            }
        }

        private void Update()
        {
            if (carrier.IsCarrying)
            {
                return; // hands full
            }

            for (int i = 0; i < slotActions.Length; i++)
            {
                if (slotActions[i] != null && slotActions[i].WasPressedThisFrame())
                {
                    Belt.Select(i);
                    break;
                }
            }
        }

        // After the camera moved this frame (PlayerController.Update), keep the item in hand attached to the view.
        private void LateUpdate()
        {
            PickupItem inHand = ItemInHand;
            if (inHand == null)
            {
                return;
            }

            inHand.transform.SetPositionAndRotation(
                cameraTransform.TransformPoint(inHand.HandPosition),
                cameraTransform.rotation * inHand.HandRotation);
        }

        /// <summary>Puts a world item onto the belt and takes it in hand if the hands are free.</summary>
        public BeltAddResult TryStore(PickupItem pickup)
        {
            BeltAddResult result = Belt.TryAdd(pickup.Item, out int slot);
            if (result != BeltAddResult.Added)
            {
                Debug.Log($"[{nameof(PlayerToolBelt)}] Cannot take '{pickup.name}': {result}", this);
                return result;
            }

            storedObjects[slot] = pickup;
            pickup.SetStored(true);
            pickup.gameObject.SetActive(false);

            if (Belt.SelectedSlot == ToolBelt.NoSelection && !carrier.IsCarrying)
            {
                Belt.Select(slot);
            }

            return result;
        }

        private void OnCarryingChanged(bool isCarrying)
        {
            if (isCarrying)
            {
                Belt.Holster();
            }
        }

        private void OnBeltChanged()
        {
            // Only the item in hand is visible; the rest stays hidden on the belt.
            for (int i = 0; i < storedObjects.Length; i++)
            {
                if (storedObjects[i] != null)
                {
                    storedObjects[i].gameObject.SetActive(i == Belt.SelectedSlot);
                }
            }

            Changed?.Invoke();
        }
    }
}
