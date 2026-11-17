using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.World;

namespace WoodcraftSimulator.Editor
{
    /// <summary>The places of the game as assets (Phase 12.5, D-056): the workshop and the customer locations.</summary>
    public static class LocationAssets
    {
        public const string Folder = "Assets/_WoodcraftSimulator/ScriptableObjects/Locations";

        public static LocationDefinition Workshop(string scenePath) =>
            GetOrCreate("Location_Workshop", LocationDefinition.WorkshopId, "Workshop", "Atölye", scenePath, 0f, 1f, 0f);

        /// <summary>Creates or updates a place (its values follow the code on every build).</summary>
        public static LocationDefinition GetOrCreate(string fileName, string id, string name, string nameTurkish,
            string scenePath, float tripCost, float materialPriceFactor, float deliveryFee)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/_WoodcraftSimulator/ScriptableObjects", "Locations");
            }

            string path = $"{Folder}/{fileName}.asset";
            var location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(path);
            if (location == null)
            {
                location = ScriptableObject.CreateInstance<LocationDefinition>();
                AssetDatabase.CreateAsset(location, path);
            }

            var serialized = new SerializedObject(location);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("displayNameTurkish").stringValue = nameTurkish;
            serialized.FindProperty("scenePath").stringValue = scenePath;
            serialized.FindProperty("tripCostEuros").floatValue = tripCost;
            serialized.FindProperty("materialPriceFactor").floatValue = materialPriceFactor;
            serialized.FindProperty("deliveryFeeEuros").floatValue = deliveryFee;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(location);
            return location;
        }
    }
}
