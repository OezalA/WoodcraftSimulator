using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// The merchant's catalog asset and the job pays (D-040). Prices are first guesses at German DIY-store level;
    /// edit them in the assets. Existing assets are not overwritten, except that a job pay of 0 gets its default.
    /// </summary>
    public static class EconomyAssets
    {
        public const string Folder = "Assets/_WoodcraftSimulator/ScriptableObjects/Economy";

        public static MaterialCatalogDefinition Catalog(IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            string path = $"{Folder}/MaterialCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<MaterialCatalogDefinition>(path);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<MaterialCatalogDefinition>();
            AssetDatabase.CreateAsset(catalog, path);

            var serialized = new SerializedObject(catalog);
            SerializedProperty boards = serialized.FindProperty("boards");
            boards.arraySize = 2;
            SetBoard(boards.GetArrayElementAtIndex(0), "spruce-2000x145x18", species["spruce"], 2000f, 145f, 18f, 8.5f);
            SetBoard(boards.GetArrayElementAtIndex(1), "spruce-2400x95x45", species["spruce"], 2400f, 95f, 45f, 9.9f);

            SerializedProperty boxes = serialized.FindProperty("fastenerBoxes");
            boxes.arraySize = 2;
            SetBox(boxes.GetArrayElementAtIndex(0), "screws-4x40-200", BlueprintAssets.WoodScrew4x40(), 200, 6f);
            SetBox(boxes.GetArrayElementAtIndex(1), "screws-5x80-50", BlueprintAssets.WoodScrew5x80(), 50, 7.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>Gives a job its default pay if none is set yet (assets created before the economy).</summary>
        public static void EnsurePay(JobDefinition job, float euros)
        {
            var serialized = new SerializedObject(job);
            SerializedProperty pay = serialized.FindProperty("payEuros");
            if (pay.floatValue <= 0f)
            {
                pay.floatValue = euros;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetBoard(SerializedProperty entry, string id, WoodSpeciesDefinition species, float length,
            float width, float thickness, float price)
        {
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("species").objectReferenceValue = species;
            entry.FindPropertyRelative("lengthMillimeters").floatValue = length;
            entry.FindPropertyRelative("widthMillimeters").floatValue = width;
            entry.FindPropertyRelative("thicknessMillimeters").floatValue = thickness;
            entry.FindPropertyRelative("priceEuros").floatValue = price;
        }

        private static void SetBox(SerializedProperty entry, string id, Object fastener, int boxSize, float price)
        {
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("fastener").objectReferenceValue = fastener;
            entry.FindPropertyRelative("boxSize").intValue = boxSize;
            entry.FindPropertyRelative("priceEuros").floatValue = price;
        }
    }
}
