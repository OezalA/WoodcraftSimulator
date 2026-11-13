using System;

namespace WoodcraftSimulator.Inventory
{
    /// <summary>
    /// Physical size class: small items fit in pockets / the tool belt; large items (timber, sheets) must be
    /// carried or stored on racks (master prompt §6.9).
    /// </summary>
    public enum ItemSizeClass
    {
        Small,
        Large
    }

    /// <summary>
    /// One physical item instance (e.g. this particular tape measure). Identity is by reference:
    /// two tape measures are two instances with the same <see cref="DefinitionId"/>.
    /// </summary>
    public sealed class InventoryItem
    {
        public InventoryItem(string definitionId, string displayName, ItemSizeClass sizeClass)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException("Item definition id must not be empty.", nameof(definitionId));
            }

            DefinitionId = definitionId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? definitionId : displayName;
            SizeClass = sizeClass;
        }

        /// <summary>Stable id of the item type, e.g. "tape-measure".</summary>
        public string DefinitionId { get; }

        public string DisplayName { get; }

        public ItemSizeClass SizeClass { get; }

        public override string ToString() => DisplayName;
    }
}
