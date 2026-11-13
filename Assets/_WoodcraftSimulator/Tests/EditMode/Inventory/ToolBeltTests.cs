using System;
using NUnit.Framework;
using WoodcraftSimulator.Inventory;

namespace WoodcraftSimulator.Tests.Inventory
{
    public sealed class ToolBeltTests
    {
        private static InventoryItem Tape() => new InventoryItem("tape-measure", "Tape measure", ItemSizeClass.Small);
        private static InventoryItem Pencil() => new InventoryItem("carpenter-pencil", "Carpenter's pencil", ItemSizeClass.Small);
        private static InventoryItem Board() => new InventoryItem("timber", "Spruce board", ItemSizeClass.Large);

        [Test]
        public void TryAdd_PutsItemsIntoFirstFreeSlots()
        {
            var belt = new ToolBelt(4);

            Assert.That(belt.TryAdd(Tape(), out int first), Is.EqualTo(BeltAddResult.Added));
            Assert.That(belt.TryAdd(Pencil(), out int second), Is.EqualTo(BeltAddResult.Added));
            Assert.That(first, Is.EqualTo(0));
            Assert.That(second, Is.EqualTo(1));
            Assert.That(belt.GetItem(1).DisplayName, Is.EqualTo("Carpenter's pencil"));
        }

        [Test]
        public void TryAdd_LargeItem_IsRejected()
        {
            var belt = new ToolBelt(4);

            Assert.That(belt.TryAdd(Board(), out int slot), Is.EqualTo(BeltAddResult.TooLarge));
            Assert.That(slot, Is.EqualTo(ToolBelt.NoSelection));
            Assert.That(belt.GetItem(0), Is.Null);
        }

        [Test]
        public void TryAdd_WhenFull_IsRejected()
        {
            var belt = new ToolBelt(1);
            belt.TryAdd(Tape(), out _);

            Assert.That(belt.IsFull, Is.True);
            Assert.That(belt.TryAdd(Pencil(), out _), Is.EqualTo(BeltAddResult.Full));
        }

        [Test]
        public void TryAdd_SameInstanceTwice_IsRejected()
        {
            var belt = new ToolBelt(4);
            InventoryItem tape = Tape();
            belt.TryAdd(tape, out _);

            Assert.That(belt.TryAdd(tape, out _), Is.EqualTo(BeltAddResult.AlreadyStored));
        }

        [Test]
        public void TryAdd_TwoInstancesOfSameType_AreBothStored()
        {
            var belt = new ToolBelt(4);

            Assert.That(belt.TryAdd(Tape(), out _), Is.EqualTo(BeltAddResult.Added));
            Assert.That(belt.TryAdd(Tape(), out _), Is.EqualTo(BeltAddResult.Added));
        }

        [Test]
        public void Select_TakesItemInHand_AndSameSlotAgainHolsters()
        {
            var belt = new ToolBelt(4);
            belt.TryAdd(Tape(), out _);

            belt.Select(0);
            Assert.That(belt.SelectedSlot, Is.EqualTo(0));
            Assert.That(belt.SelectedItem.DefinitionId, Is.EqualTo("tape-measure"));

            belt.Select(0);
            Assert.That(belt.SelectedSlot, Is.EqualTo(ToolBelt.NoSelection));
            Assert.That(belt.SelectedItem, Is.Null);
        }

        [Test]
        public void Select_EmptySlot_Holsters()
        {
            var belt = new ToolBelt(4);
            belt.TryAdd(Tape(), out _);
            belt.Select(0);

            belt.Select(3);

            Assert.That(belt.SelectedSlot, Is.EqualTo(ToolBelt.NoSelection));
        }

        [Test]
        public void Select_OtherSlot_SwitchesItemInHand()
        {
            var belt = new ToolBelt(4);
            belt.TryAdd(Tape(), out _);
            belt.TryAdd(Pencil(), out _);
            belt.Select(0);

            belt.Select(1);

            Assert.That(belt.SelectedItem.DefinitionId, Is.EqualTo("carpenter-pencil"));
        }

        [Test]
        public void TryRemove_SelectedItem_EmptiesHands()
        {
            var belt = new ToolBelt(4);
            belt.TryAdd(Tape(), out _);
            belt.Select(0);

            Assert.That(belt.TryRemove(0, out InventoryItem removed), Is.True);
            Assert.That(removed.DefinitionId, Is.EqualTo("tape-measure"));
            Assert.That(belt.SelectedSlot, Is.EqualTo(ToolBelt.NoSelection));
            Assert.That(belt.TryRemove(0, out _), Is.False);
        }

        [Test]
        public void Changed_IsRaisedOnlyOnActualChanges()
        {
            var belt = new ToolBelt(2);
            int changes = 0;
            belt.Changed += () => changes++;

            belt.TryAdd(Tape(), out _);   // change
            belt.Holster();               // already holstered: no change
            belt.Select(0);               // change
            belt.Select(1);               // empty slot → holster: change
            belt.TryAdd(Board(), out _);  // rejected: no change

            Assert.That(changes, Is.EqualTo(3));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidSlot_Throws(int slot)
        {
            var belt = new ToolBelt(4);

            Assert.Throws<ArgumentOutOfRangeException>(() => belt.Select(slot));
            Assert.Throws<ArgumentOutOfRangeException>(() => belt.GetItem(slot));
        }

        [Test]
        public void Constructor_WithoutSlots_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ToolBelt(0));
        }
    }
}
