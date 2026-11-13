using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Creates the initial wood species definition assets (master prompt §6.5) if they don't exist yet.
    /// Existing assets are never overwritten: once created, the asset is the source of truth and can be edited.
    /// </summary>
    public static class WoodSpeciesAssets
    {
        public const string Folder = "Assets/_WoodcraftSimulator/ScriptableObjects/WoodSpecies";

        private const string DataNotes =
            "Approximate typical air-dry density (~12 % moisture content), rounded. Natural variation is " +
            "roughly ±10 % or more. Placeholder reference value: verify against published wood property " +
            "tables before relying on it for gameplay that claims real-world accuracy.";

        private readonly struct SpeciesSeed
        {
            public SpeciesSeed(string id, string displayName, string scientificName, float density, Color color)
            {
                Id = id;
                DisplayName = displayName;
                ScientificName = scientificName;
                Density = density;
                Color = color;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string ScientificName { get; }
            public float Density { get; }
            public Color Color { get; }
        }

        private static readonly SpeciesSeed[] Seeds =
        {
            new SpeciesSeed("spruce", "Spruce", "Picea abies", 450f, new Color(0.90f, 0.80f, 0.62f)),
            new SpeciesSeed("pine", "Pine", "Pinus sylvestris", 520f, new Color(0.88f, 0.72f, 0.50f)),
            new SpeciesSeed("oak", "Oak", "Quercus robur", 710f, new Color(0.70f, 0.55f, 0.36f)),
            new SpeciesSeed("beech", "Beech", "Fagus sylvatica", 720f, new Color(0.85f, 0.66f, 0.50f)),
            new SpeciesSeed("walnut", "Walnut", "Juglans regia", 640f, new Color(0.45f, 0.32f, 0.22f)),
            new SpeciesSeed("larch", "Larch", "Larix decidua", 590f, new Color(0.82f, 0.58f, 0.40f)),
            new SpeciesSeed("douglas-fir", "Douglas fir", "Pseudotsuga menziesii", 530f, new Color(0.84f, 0.60f, 0.42f)),
        };

        /// <summary>Returns all default species by id, creating missing assets.</summary>
        public static Dictionary<string, WoodSpeciesDefinition> EnsureDefaults()
        {
            var result = new Dictionary<string, WoodSpeciesDefinition>();

            foreach (SpeciesSeed seed in Seeds)
            {
                string path = $"{Folder}/{seed.DisplayName.Replace(' ', '_')}.asset";
                var definition = AssetDatabase.LoadAssetAtPath<WoodSpeciesDefinition>(path);

                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<WoodSpeciesDefinition>();
                    AssetDatabase.CreateAsset(definition, path);

                    var serialized = new SerializedObject(definition);
                    serialized.FindProperty("id").stringValue = seed.Id;
                    serialized.FindProperty("displayName").stringValue = seed.DisplayName;
                    serialized.FindProperty("scientificName").stringValue = seed.ScientificName;
                    serialized.FindProperty("densityKgPerCubicMeter").floatValue = seed.Density;
                    serialized.FindProperty("greyboxColor").colorValue = seed.Color;
                    serialized.FindProperty("dataNotes").stringValue = DataNotes;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssetIfDirty(definition);
                }

                result[definition.Id] = definition;
            }

            return result;
        }
    }
}
