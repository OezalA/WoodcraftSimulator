using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Economy
{
    /// <summary>
    /// What the timber merchant sells, as data (D-040): stock boards and fastener boxes with prices in euros.
    /// Converted to the domain <see cref="MaterialCatalog"/>; prices are edited here, not in code.
    /// </summary>
    [CreateAssetMenu(menuName = "WoodcraftSimulator/Material Catalog", fileName = "MaterialCatalog")]
    public sealed class MaterialCatalogDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class BoardEntry
        {
            public string id = "spruce-2000x145x18";
            public WoodSpeciesDefinition species;
            [Min(1f)] public float lengthMillimeters = 2000f;
            [Min(1f)] public float widthMillimeters = 145f;
            [Min(1f)] public float thicknessMillimeters = 18f;
            [Min(0.01f)] public float priceEuros = 8.5f;

            public BoardDimensions Dimensions =>
                BoardDimensions.FromMillimeters(lengthMillimeters, widthMillimeters, thicknessMillimeters);
        }

        [Serializable]
        public sealed class FastenerBoxEntry
        {
            public string id = "screws-4x40-200";
            public FastenerDefinition fastener;
            [Min(1)] public int boxSize = 200;
            [Min(0.01f)] public float priceEuros = 6f;
        }

        [SerializeField] private BoardEntry[] boards = Array.Empty<BoardEntry>();
        [SerializeField] private FastenerBoxEntry[] fastenerBoxes = Array.Empty<FastenerBoxEntry>();

        public IReadOnlyList<BoardEntry> Boards => boards;

        public IReadOnlyList<FastenerBoxEntry> FastenerBoxes => fastenerBoxes;

        public MaterialCatalog ToDomain()
        {
            var items = new List<CatalogItem>();
            foreach (BoardEntry board in boards)
            {
                if (board?.species == null)
                {
                    continue;
                }

                items.Add(CatalogItem.Board(board.id,
                    new StockSpec(board.species.Id, board.species.DisplayName, board.Dimensions), Price(board.priceEuros)));
            }

            foreach (FastenerBoxEntry box in fastenerBoxes)
            {
                if (box?.fastener == null)
                {
                    continue;
                }

                string name = Loc.T($"Box of {box.boxSize} {box.fastener.DisplayName}",
                    $"{box.boxSize} adet {box.fastener.DisplayName} kutusu");
                items.Add(CatalogItem.FastenerBox(box.id, name, box.fastener.Id, box.boxSize, Price(box.priceEuros)));
            }

            return new MaterialCatalog(items);
        }

        /// <summary>The species asset of a board item (to create the physical board), or null.</summary>
        public WoodSpeciesDefinition SpeciesOf(string boardItemId)
        {
            foreach (BoardEntry board in boards)
            {
                if (board != null && board.id == boardItemId)
                {
                    return board.species;
                }
            }

            return null;
        }

        // float → decimal via the shortest string avoids 8.5f becoming 8.4999...
        private static Money Price(float euros) =>
            Money.FromEuros(decimal.Parse(euros.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.CultureInfo.InvariantCulture));
    }
}
