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

        [Tooltip("Full pay in euros when every quality goal is met (D-040: 70 % for delivering, 30 % for the goals).")]
        [SerializeField, Min(0f)] private float payEuros;

        [Tooltip("The customer wants every part sanded and oiled (extra quality goal, D-045).")]
        [SerializeField] private bool requiresFinish;

        [Serializable]
        public sealed class ExtraStock
        {
            public WoodSpeciesDefinition species;
            [Min(1f)] public float lengthMillimeters = 2400f;
            [Min(1f)] public float widthMillimeters = 95f;
            [Min(1f)] public float thicknessMillimeters = 45f;
        }

        [Tooltip("Further stocks for jobs with parts of several sizes (terrace, D-046); each part uses the best fit.")]
        [SerializeField] private ExtraStock[] extraStocks = Array.Empty<ExtraStock>();

        [Tooltip("Share of the pay the customer pays on acceptance, for the material (0..0.7, D-046).")]
        [SerializeField, Range(0f, 0.7f)] private float depositShare;

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
            new StockSpec(stockSpecies.Id, stockSpecies.DisplayName, StockDimensions), spareBoards, Tips(),
            Economy.Money.FromEuros((decimal)Math.Round(payEuros, 2)), requiresFinish, ExtraStocks(), depositShare);

        /// <summary>All stocks with their species assets (the main one first), to deliver bought boards.</summary>
        public IEnumerable<(StockSpec stock, WoodSpeciesDefinition species)> StockAssets()
        {
            yield return (new StockSpec(stockSpecies.Id, stockSpecies.DisplayName, StockDimensions), stockSpecies);
            foreach (ExtraStock extra in extraStocks)
            {
                if (extra?.species != null)
                {
                    yield return (new StockSpec(extra.species.Id, extra.species.DisplayName,
                        BoardDimensions.FromMillimeters(extra.lengthMillimeters, extra.widthMillimeters, extra.thicknessMillimeters)),
                        extra.species);
                }
            }
        }

        private IEnumerable<StockSpec> ExtraStocks()
        {
            bool first = true;
            foreach ((StockSpec stock, WoodSpeciesDefinition _) in StockAssets())
            {
                if (!first)
                {
                    yield return stock;
                }

                first = false;
            }
        }

        private IEnumerable<string> Tips()
        {
            for (int i = 0; i < tips.Length; i++)
            {
                yield return Loc.Pick(tips[i], tipsTurkish != null && i < tipsTurkish.Length ? tipsTurkish[i] : null);
            }
        }
    }
}
