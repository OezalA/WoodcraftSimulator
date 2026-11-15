using System;

namespace WoodcraftSimulator.Materials
{
    /// <summary>A stock board as bought: species and nominal size of one board (jobs, catalog).</summary>
    public sealed class StockSpec
    {
        public StockSpec(string speciesId, string speciesName, BoardDimensions dimensions)
        {
            if (string.IsNullOrWhiteSpace(speciesId))
            {
                throw new ArgumentException("Species id must not be empty.", nameof(speciesId));
            }

            SpeciesId = speciesId;
            SpeciesName = string.IsNullOrWhiteSpace(speciesName) ? speciesId : speciesName;
            Dimensions = dimensions;
        }

        public string SpeciesId { get; }

        public string SpeciesName { get; }

        public BoardDimensions Dimensions { get; }

        /// <summary>Same species and exactly the same size.</summary>
        public bool Matches(string speciesId, BoardDimensions dimensions) =>
            SpeciesId == speciesId && Dimensions == dimensions;

        public override string ToString() =>
            Localization.Loc.T($"{SpeciesName} board {Dimensions}", $"{SpeciesName} tahta {Dimensions}");
    }
}
