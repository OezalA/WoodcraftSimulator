using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.World;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// The first two customer locations and their jobs (Phase 12.5, D-055/D-056): a terrace at a mountain house with a
    /// view of snowy peaks, and a deck with a ready-made glass railing at an island house by the sea. Both use the
    /// terrace's construction (a variant blueprint each, so each place's station knows its own).
    /// </summary>
    public static class LocationJobs
    {
        public const string ScenesFolder = "Assets/_WoodcraftSimulator/Scenes/Locations";
        public const string MountainScenePath = ScenesFolder + "/MountainHouse.unity";
        public const string IslandScenePath = ScenesFolder + "/IslandHouse.unity";
        public const string MountainId = "mountain";
        public const string IslandId = "island";

        public static LocationDefinition Mountain() =>
            LocationAssets.GetOrCreate("Location_Mountain", MountainId, "Mountain house", "Dağ evi", MountainScenePath,
                tripCost: 12f, materialPriceFactor: 1f, deliveryFee: 20f);

        public static LocationDefinition Island() =>
            LocationAssets.GetOrCreate("Location_Island", IslandId, "Island house", "Ada evi", IslandScenePath,
                tripCost: 25f, materialPriceFactor: 1.3f, deliveryFee: 40f);

        public static BlueprintDefinition MountainTerrace() =>
            TerraceAssets.Terrace("MountainTerrace2400x1800", "mountain-terrace", "Mountain terrace 2.4 × 1.8 m",
                "Dağ terası 2,4 × 1,8 m");

        public static BlueprintDefinition IslandDeck() =>
            TerraceAssets.Terrace("IslandDeck2400x1800", "island-deck-glass", "Sea view deck with glass railing",
                "Cam korkuluklu deniz verandası", glassRailing: true);

        public static JobDefinition MountainTerraceJob(IReadOnlyDictionary<string, WoodSpeciesDefinition> species) =>
            GetOrCreate("Job_MountainTerrace", "mountain-terrace-job", "Mountain house terrace", "Dağ evi terası",
                "A terrace in front of a mountain house, looking at the snowy peaks: pier blocks, beams, joists, larch decking.",
                "Dağ evinin önünde, karlı zirvelere bakan teras: beton ayaklar, kirişler, döşeme altı kirişleri, melez döşeme.",
                MountainTerrace(), Mountain(), species, 1100f, glass: false);

        public static JobDefinition IslandDeckJob(IReadOnlyDictionary<string, WoodSpeciesDefinition> species) =>
            GetOrCreate("Job_IslandDeck", "island-deck-job", "Island house deck with glass railing",
                "Ada evi cam korkuluklu veranda",
                "A deck at an island house facing the sea, with a ready-made glass railing on its front edge (three panels, two clamps each).",
                "Denize bakan ada evinde veranda; ön kenarında hazır cam korkuluk (üç panel, her biri iki kelepçeyle).",
                IslandDeck(), Island(), species, 1650f, glass: true);

        private static JobDefinition GetOrCreate(string fileName, string id, string name, string nameTurkish,
            string description, string descriptionTurkish, BlueprintDefinition blueprint, LocationDefinition location,
            IReadOnlyDictionary<string, WoodSpeciesDefinition> species, float pay, bool glass)
        {
            string path = $"{JobAssets.Folder}/{fileName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<JobDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            // Written on every build (data defined here only).
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("displayNameTurkish").stringValue = nameTurkish;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("descriptionTurkish").stringValue = descriptionTurkish;
            serialized.FindProperty("blueprint").objectReferenceValue = blueprint;
            serialized.FindProperty("location").objectReferenceValue = location;
            serialized.FindProperty("stockSpecies").objectReferenceValue = species["larch"];
            serialized.FindProperty("stockLengthMillimeters").floatValue = 2400f;
            serialized.FindProperty("stockWidthMillimeters").floatValue = 145f;
            serialized.FindProperty("stockThicknessMillimeters").floatValue = 28f;
            serialized.FindProperty("payEuros").floatValue = pay;
            serialized.FindProperty("depositShare").floatValue = 0.5f;

            SerializedProperty extras = serialized.FindProperty("extraStocks");
            extras.arraySize = glass ? 4 : 3;
            SetExtra(extras.GetArrayElementAtIndex(0), species["spruce"], 2400f, 145f, 45f);
            SetExtra(extras.GetArrayElementAtIndex(1), species["spruce"], 2400f, 120f, 45f);
            SetExtra(extras.GetArrayElementAtIndex(2), species["concrete"], 200f, 200f, 200f);
            if (glass)
            {
                SetExtra(extras.GetArrayElementAtIndex(3), species["glass"], 790f, 1000f, 10f);
            }

            var tips = new List<string>
            {
                "Drive there with the van (it stands in the workshop yard). The material is delivered to the site; take your tools (the belt, or loaded in the van bed) and the stepladder if you need it.",
                "Set out at the corner stake, prepare the pier spots, then build bottom up; the customer inspects each stage (E on the corner stake).",
                "Hand the finished job over at the house door."
            };
            var tipsTurkish = new List<string>
            {
                "Oraya kamyonetle git (atölye avlusunda durur). Malzeme şantiyeye teslim edilir; aletlerini al (kemerde ya da kamyonet kasasında), gerekirse merdiveni de.",
                "Köşe kazığında alanı işaretle, ayak yerlerini hazırla, aşağıdan yukarı kur; müşteri her aşamayı denetler (köşe kazığında E).",
                "Bitince işi evin kapısında müşteriye teslim et."
            };
            if (glass)
            {
                tips.Insert(2, "The glass panels come ready-made: carry one to the front edge of the deck and fix it with its two clamps (drill-driver).");
                tipsTurkish.Insert(2, "Cam paneller hazır gelir: birini döşemenin ön kenarına taşı, iki kelepçesiyle sabitle (şarjlı matkap).");
            }

            SetStrings(serialized.FindProperty("tips"), tips);
            SetStrings(serialized.FindProperty("tipsTurkish"), tipsTurkish);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        private static void SetExtra(SerializedProperty extra, WoodSpeciesDefinition species, float length, float width,
            float thickness)
        {
            extra.FindPropertyRelative("species").objectReferenceValue = species;
            extra.FindPropertyRelative("lengthMillimeters").floatValue = length;
            extra.FindPropertyRelative("widthMillimeters").floatValue = width;
            extra.FindPropertyRelative("thicknessMillimeters").floatValue = thickness;
        }

        private static void SetStrings(SerializedProperty array, IReadOnlyList<string> values)
        {
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                array.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }
    }
}
