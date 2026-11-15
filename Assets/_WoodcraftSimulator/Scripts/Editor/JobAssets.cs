using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Creates the Phase 6 job assets if they don't exist yet (D-039). Existing assets are never overwritten: once
    /// created, the asset is the source of truth. Material quantities are not entered here: the game calculates them.
    /// </summary>
    public static class JobAssets
    {
        public const string Folder = "Assets/_WoodcraftSimulator/ScriptableObjects/Jobs";

        public static JobDefinition FrameJob(IReadOnlyDictionary<string, WoodSpeciesDefinition> species) =>
            GetOrCreate("Job_Frame600x300", "frame-600x300-job", "Practice frame",
                "A 600 × 300 mm frame from construction timber: two rails, two sides between them, two 5 × 80 mm " +
                "screws per corner.",
                BlueprintAssets.Frame600x300(), species["spruce"], 2400f, 95f, 45f, spareBoards: 0,
                new[]
                {
                    "Hook the tape on the end you measure from; keep the kerf on the waste side of the line.",
                    "The corner screws are close to the rail ends: pre-drill (Q switches to the drill bit) or the rail splits."
                });

        public static JobDefinition ShelfJob(IReadOnlyDictionary<string, WoodSpeciesDefinition> species) =>
            GetOrCreate("Job_Shelf500x600", "shelf-500x600-job", "Small shelf",
                "A standing shelf 500 wide, 600 high, 145 deep from 18 mm spruce boards: two sides, three shelves " +
                "between them, two 4 × 40 mm screws per joint.",
                BlueprintAssets.WallShelf(), species["spruce"], 2000f, 145f, 18f, spareBoards: 0,
                new[]
                {
                    "Top and bottom shelves are screwed 9 mm from the ends of the sides: pre-drill those.",
                    "Start with a low clutch and raise it until the screw stops flush by itself."
                });

        private static JobDefinition GetOrCreate(string fileName, string id, string displayName, string description,
            BlueprintDefinition blueprint, WoodSpeciesDefinition stockSpecies, float lengthMm, float widthMm,
            float thicknessMm, int spareBoards, string[] tips)
        {
            string path = $"{Folder}/{fileName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<JobDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("blueprint").objectReferenceValue = blueprint;
            serialized.FindProperty("stockSpecies").objectReferenceValue = stockSpecies;
            serialized.FindProperty("stockLengthMillimeters").floatValue = lengthMm;
            serialized.FindProperty("stockWidthMillimeters").floatValue = widthMm;
            serialized.FindProperty("stockThicknessMillimeters").floatValue = thicknessMm;
            serialized.FindProperty("spareBoards").intValue = spareBoards;
            SerializedProperty tipsProperty = serialized.FindProperty("tips");
            tipsProperty.arraySize = tips.Length;
            for (int i = 0; i < tips.Length; i++)
            {
                tipsProperty.GetArrayElementAtIndex(i).stringValue = tips[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }
    }
}
