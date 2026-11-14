using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Inventory;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Creates the initial small-item definition assets if they don't exist yet.
    /// Existing assets are never overwritten: once created, the asset is the source of truth.
    /// </summary>
    public static class ItemAssets
    {
        public const string Folder = "Assets/_WoodcraftSimulator/ScriptableObjects/Items";

        public static ItemDefinition TapeMeasure() =>
            GetOrCreate("TapeMeasure", "tape-measure", "Tape measure", ItemSizeClass.Small);

        // A cordless drill-driver on the belt (Small) in this prototype, like the saw.
        public static ItemDefinition DrillDriver() =>
            GetOrCreate("DrillDriver", "drill-driver", "Drill-driver", ItemSizeClass.Small);

        // A hand saw hangs on the belt in this prototype (Small); a proper tool wall / toolbox comes later.
        public static ItemDefinition HandSaw() =>
            GetOrCreate("HandSaw", "hand-saw", "Hand saw", ItemSizeClass.Small);

        public static ItemDefinition CarpenterPencil() =>
            GetOrCreate("CarpenterPencil", "carpenter-pencil", "Carpenter's pencil", ItemSizeClass.Small);

        private static ItemDefinition GetOrCreate(string fileName, string id, string displayName, ItemSizeClass size)
        {
            string path = $"{Folder}/{fileName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(definition, path);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("sizeClass").enumValueIndex = (int)size;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }
    }
}
