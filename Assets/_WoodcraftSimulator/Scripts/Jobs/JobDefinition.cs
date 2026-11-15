using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Jobs
{
    /// <summary>
    /// A job as data (ScriptableObject asset): what to build (blueprint), the stock it is made from, spare boards and
    /// tips. Converted to the domain <see cref="Job"/>; the material quantities are calculated, not entered (D-039).
    /// Texts have a Turkish version (D-041); an empty Turkish text falls back to the English one.
    /// </summary>
    [CreateAssetMenu(menuName = "WoodcraftSimulator/Job", fileName = "Job")]
    public sealed class JobDefinition : ScriptableObject
    {
        [SerializeField] private string id = "job";
        [SerializeField] private string displayName = "Job";
        [SerializeField, TextArea] private string description = string.Empty;
        [SerializeField] private BlueprintDefinition blueprint;

        [Header("Stock (one board as bought)")]
        [SerializeField] private WoodSpeciesDefinition stockSpecies;
        [SerializeField, Min(1f)] private float stockLengthMillimeters = 2000f;
        [SerializeField, Min(1f)] private float stockWidthMillimeters = 145f;
        [SerializeField, Min(1f)] private float stockThicknessMillimeters = 18f;

        [Tooltip("Boards delivered on top of what the material plan needs.")]
        [SerializeField, Min(0)] private int spareBoards;

        [SerializeField] private string[] tips = Array.Empty<string>();

        [Header("Turkish (D-041)")]
        [SerializeField] private string displayNameTurkish = string.Empty;
        [SerializeField, TextArea] private string descriptionTurkish = string.Empty;

        [Tooltip("Same order as Tips; a missing or empty entry shows the English tip.")]
        [SerializeField] private string[] tipsTurkish = Array.Empty<string>();

        public string Id => id;

        public string DisplayName => Loc.Pick(displayName, displayNameTurkish);

        public BlueprintDefinition Blueprint => blueprint;

        public WoodSpeciesDefinition StockSpecies => stockSpecies;

        public BoardDimensions StockDimensions =>
            BoardDimensions.FromMillimeters(stockLengthMillimeters, stockWidthMillimeters, stockThicknessMillimeters);

        public Job ToDomain() => new Job(id, DisplayName, Loc.Pick(description, descriptionTurkish), blueprint.ToDomain(),
            new StockSpec(stockSpecies.Id, stockSpecies.DisplayName, StockDimensions), spareBoards, Tips());

        private IEnumerable<string> Tips()
        {
            for (int i = 0; i < tips.Length; i++)
            {
                yield return Loc.Pick(tips[i], tipsTurkish != null && i < tipsTurkish.Length ? tipsTurkish[i] : null);
            }
        }
    }
}
