using UnityEngine;

namespace WoodcraftSimulator.Inventory
{
    /// <summary>
    /// Static definition of an item type (ScriptableObject asset): id, name, size class.
    /// Each scene object creates its own <see cref="InventoryItem"/> instance from it.
    /// Tool-specific data (precision, tolerance, ...) will be added when the tools need it (Phase 3b/3c).
    /// </summary>
    [CreateAssetMenu(menuName = "WoodcraftSimulator/Item Definition", fileName = "Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier used by data and save files, e.g. \"tape-measure\".")]
        [SerializeField] private string id = "item";

        [SerializeField] private string displayName = "Item";

        [Tooltip("Turkish name (D-041); empty = the English name is shown.")]
        [SerializeField] private string displayNameTurkish = string.Empty;

        [SerializeField] private ItemSizeClass sizeClass = ItemSizeClass.Small;

        public string Id => id;

        public string DisplayName => Localization.Loc.Pick(displayName, displayNameTurkish);

        public ItemSizeClass SizeClass => sizeClass;

        public InventoryItem CreateInstance() => new InventoryItem(id, DisplayName, sizeClass);
    }
}
