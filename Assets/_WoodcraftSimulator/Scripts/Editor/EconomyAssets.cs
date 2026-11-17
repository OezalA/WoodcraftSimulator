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
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MaterialCatalogDefinition>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            // Entries are added when missing (by id); existing entries and their prices are kept.
            var serialized = new SerializedObject(catalog);
            SerializedProperty boards = serialized.FindProperty("boards");
            EnsureBoard(boards, "spruce-2000x145x18", species["spruce"], 2000f, 145f, 18f, 8.5f);
            EnsureBoard(boards, "spruce-2400x95x45", species["spruce"], 2400f, 95f, 45f, 9.9f);

            // Woods customers may ask for (D-054): the bench stock sizes in pine and hardwoods.
            EnsureBoard(boards, "pine-2400x95x45", species["pine"], 2400f, 95f, 45f, 8.5f);
            EnsureBoard(boards, "beech-2000x145x18", species["beech"], 2000f, 145f, 18f, 19f);
            EnsureBoard(boards, "oak-2000x145x18", species["oak"], 2000f, 145f, 18f, 26f);
            EnsureBoard(boards, "walnut-2000x145x18", species["walnut"], 2000f, 145f, 18f, 38f);

            // Terrace material (D-046), delivered to the site outside.
            EnsureBoard(boards, "larch-2400x145x28", species["larch"], 2400f, 145f, 28f, 12f, outdoors: true);
            EnsureBoard(boards, "spruce-2400x145x45", species["spruce"], 2400f, 145f, 45f, 14f, outdoors: true);
            EnsureBoard(boards, "spruce-2400x120x45", species["spruce"], 2400f, 120f, 45f, 11f, outdoors: true);
            EnsureBoard(boards, "concrete-pier-200", species["concrete"], 200f, 200f, 200f, 4.5f, outdoors: true);

            // Pergola material (D-049), also delivered outside.
            EnsureBoard(boards, "spruce-3000x120x45", species["spruce"], 3000f, 120f, 45f, 15f, outdoors: true);
            EnsureBoard(boards, "spruce-3000x145x45", species["spruce"], 3000f, 145f, 45f, 18f, outdoors: true);
            EnsureBoard(boards, "spruce-2400x100x100", species["spruce"], 2400f, 100f, 100f, 22f, outdoors: true);

            // Ready-made glass railing panels for the island balcony (D-055), delivered where the job is.
            EnsureBoard(boards, "glass-panel-790x1000x10", species["glass"], 790f, 1000f, 10f, 85f, outdoors: true);

            SerializedProperty boxes = serialized.FindProperty("fastenerBoxes");
            EnsureBox(boxes, "screws-4x40-200", BlueprintAssets.WoodScrew4x40(), 200, 6f);
            EnsureBox(boxes, "screws-5x80-50", BlueprintAssets.WoodScrew5x80(), 50, 7.5f);
            EnsureBox(boxes, "nails-2.8x50-250", BlueprintAssets.Nail2_8x50(), 250, 4f);
            EnsureBox(boxes, "dowels-8x40-50", BlueprintAssets.Dowel8x40(), 50, 3.5f);
            EnsureBox(boxes, "deck-screws-5x60-200", TerraceAssets.DeckScrew(), 200, 18f);
            EnsureBox(boxes, "glass-clamps-6", TerraceAssets.GlassClamp(), 6, 24f);
            EnsureBox(boxes, "joist-hangers-10", TerraceAssets.JoistHanger(), 10, 15f);
            EnsureBox(boxes, "post-bases-6", TerraceAssets.PostBase(), 6, 12f);
            EnsureBox(boxes, "post-anchors-4", PergolaAssets.PostAnchor(), 4, 28f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>Adds a board entry unless one with this id exists.</summary>
        public static void EnsureBoard(SerializedProperty boards, string id, WoodSpeciesDefinition species, float length,
            float width, float thickness, float price, bool outdoors = false)
        {
            if (IndexOf(boards, id) < 0)
            {
                boards.arraySize++;
                SerializedProperty entry = boards.GetArrayElementAtIndex(boards.arraySize - 1);
                SetBoard(entry, id, species, length, width, thickness, price);
                entry.FindPropertyRelative("deliverOutdoors").boolValue = outdoors;
            }
        }

        private static void EnsureBox(SerializedProperty boxes, string id, Object fastener, int boxSize, float price)
        {
            if (IndexOf(boxes, id) < 0)
            {
                boxes.arraySize++;
                SetBox(boxes.GetArrayElementAtIndex(boxes.arraySize - 1), id, fastener, boxSize, price);
            }
        }

        private static int IndexOf(SerializedProperty array, string id)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id)
                {
                    return i;
                }
            }

            return -1;
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
