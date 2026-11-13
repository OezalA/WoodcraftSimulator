using System;

namespace WoodcraftSimulator.Inventory
{
    public enum BeltAddResult
    {
        Added,
        /// <summary>All slots are occupied.</summary>
        Full,
        /// <summary>Only small items fit on the belt.</summary>
        TooLarge,
        /// <summary>This exact item instance is already on the belt.</summary>
        AlreadyStored
    }

    /// <summary>
    /// The player's tool belt: a fixed number of slots for small items, at most one of them in hand (selected).
    /// Rules: large items are rejected; selecting the already selected slot or an empty slot holsters (empty hands).
    /// </summary>
    public sealed class ToolBelt
    {
        public const int NoSelection = -1;

        private readonly InventoryItem[] slots;

        public ToolBelt(int slotCount)
        {
            if (slotCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "A tool belt needs at least one slot.");
            }

            slots = new InventoryItem[slotCount];
        }

        public int SlotCount => slots.Length;

        /// <summary>Index of the slot whose item is in hand, or <see cref="NoSelection"/>.</summary>
        public int SelectedSlot { get; private set; } = NoSelection;

        /// <summary>The item in hand, or null.</summary>
        public InventoryItem SelectedItem => SelectedSlot == NoSelection ? null : slots[SelectedSlot];

        public bool IsFull => Array.IndexOf(slots, null) < 0;

        /// <summary>Raised after any change of contents or selection.</summary>
        public event Action Changed;

        public InventoryItem GetItem(int slot)
        {
            RequireValidSlot(slot);
            return slots[slot];
        }

        /// <summary>Puts the item into the first free slot.</summary>
        public BeltAddResult TryAdd(InventoryItem item, out int slot)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            slot = NoSelection;

            if (item.SizeClass != ItemSizeClass.Small)
            {
                return BeltAddResult.TooLarge;
            }

            if (Array.IndexOf(slots, item) >= 0)
            {
                return BeltAddResult.AlreadyStored;
            }

            int free = Array.IndexOf(slots, null);
            if (free < 0)
            {
                return BeltAddResult.Full;
            }

            slots[free] = item;
            slot = free;
            Changed?.Invoke();
            return BeltAddResult.Added;
        }

        /// <summary>Takes the item out of the slot. If it was in hand, the hands become empty.</summary>
        public bool TryRemove(int slot, out InventoryItem item)
        {
            RequireValidSlot(slot);
            item = slots[slot];
            if (item == null)
            {
                return false;
            }

            slots[slot] = null;
            if (SelectedSlot == slot)
            {
                SelectedSlot = NoSelection;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Takes the slot's item in hand. Selecting the slot that is already in hand, or an empty slot, holsters.
        /// </summary>
        public void Select(int slot)
        {
            RequireValidSlot(slot);
            int newSelection = slot == SelectedSlot || slots[slot] == null ? NoSelection : slot;
            SetSelection(newSelection);
        }

        /// <summary>Puts the item in hand back on the belt (empty hands).</summary>
        public void Holster() => SetSelection(NoSelection);

        private void SetSelection(int slot)
        {
            if (slot == SelectedSlot)
            {
                return;
            }

            SelectedSlot = slot;
            Changed?.Invoke();
        }

        private void RequireValidSlot(int slot)
        {
            if (slot < 0 || slot >= slots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Slot must be between 0 and {slots.Length - 1}.");
            }
        }
    }
}
