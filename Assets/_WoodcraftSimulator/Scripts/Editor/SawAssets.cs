using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Cutting;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Creates the saw definition assets if they don't exist yet (folder created by the scene builder). Never overwrites:
    /// once created, the asset is the source of truth. Values are approximate placeholders (D-029).
    /// </summary>
    public static class SawAssets
    {
        public const string Folder = "Assets/_WoodcraftSimulator/ScriptableObjects/Saws";

        public static SawDefinition HandSaw() => GetOrCreate("HandSaw", "hand-saw", "Hand saw", 2f);

        private static SawDefinition GetOrCreate(string fileName, string id, string displayName, float kerfMillimeters)
        {
            string path = $"{Folder}/{fileName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<SawDefinition>(path);
            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<SawDefinition>();
            AssetDatabase.CreateAsset(definition, path);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("kerfMillimeters").floatValue = kerfMillimeters;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }
    }
}
