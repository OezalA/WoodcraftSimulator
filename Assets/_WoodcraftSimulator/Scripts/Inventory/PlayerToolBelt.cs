using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Tools;

namespace WoodcraftSimulator.Inventory
{
    /// <summary>
    /// Scene side of the player's <see cref="ToolBelt"/>: stores <see cref="PickupItem"/> objects, selects them
    /// with the ToolSlot1..N keys, and shows the selected one in hand.
    /// Hands-full rule: picking up a large object holsters the tool, and slots can't be switched while carrying;
    /// the tool comes back into the hand when the object is put down.
    /// </summary>
    public sealed class PlayerToolBelt : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlayerCarrier carrier;
        [SerializeField, Min(1)] private int slotCount = 4;

        private InputAction[] slotActions;
        private PickupItem[] storedObjects;
        private HeldToolContext toolContext;
        private PickupItem equippedObject;
        private int slotBeforeCarry = ToolBelt.NoSelection;

        /// <summary>The tool behaviour of the item in hand, or null (empty hands or an item without one).</summary>
        public IHeldTool HeldTool { get; private set; }

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
            toolContext = new HeldToolContext(cameraTransform, transform, Belt);
            Belt.Changed += OnBeltChanged;

            slotActions = new InputAction[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                // Missing actions (more slots than bound keys) are tolerated: that slot just has no hotkey.
                slotActions[i] = InputSystem.actions.FindAction($"Player/ToolSlot{i + 1}");
            }

            carrier.CarryingChanged += OnCarryingChanged;
        }

        // Paused (PauseController disables this component): the tool in hand must stop working too.
        private void OnEnable() => HeldTool?.Equip(toolContext);

        private void OnDisable() => HeldTool?.Unequip();

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

            // The item just taken goes straight into the hand (the previous one goes onto the belt).
            if (!carrier.IsCarrying)
            {
                Belt.Select(slot);
            }

            return result;
        }

        private void OnCarryingChanged(bool isCarrying)
        {
            if (isCarrying)
            {
                // Hands full: holster, but remember the tool so it comes back after the object is put down.
                slotBeforeCarry = Belt.SelectedSlot;
                Belt.Holster();
            }
            else if (slotBeforeCarry != ToolBelt.NoSelection)
            {
                int slot = slotBeforeCarry;
                slotBeforeCarry = ToolBelt.NoSelection;
                if (Belt.SelectedSlot == ToolBelt.NoSelection && Belt.GetItem(slot) != null)
                {
                    Belt.Select(slot);
                }
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

            UpdateEquippedTool();
            Changed?.Invoke();
        }

        /// <summary>Unequips the previous tool and equips the one now in hand (if the item has tool behaviour).</summary>
        private void UpdateEquippedTool()
        {
            PickupItem inHand = ItemInHand;
            if (inHand == equippedObject)
            {
                return;
            }

            HeldTool?.Unequip();
            equippedObject = inHand;
            HeldTool = inHand != null ? inHand.GetComponent<IHeldTool>() : null;
            HeldTool?.Equip(toolContext);
        }
    }
}
