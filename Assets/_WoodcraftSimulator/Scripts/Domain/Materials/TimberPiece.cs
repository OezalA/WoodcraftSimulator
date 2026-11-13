using System;

namespace WoodcraftSimulator.Materials
{
    /// <summary>
    /// Authoritative state of one physical piece of rectangular timber.
    /// Immutable: operations such as cutting (Phase 4) will produce new pieces instead of mutating this one.
    /// </summary>
    public sealed class TimberPiece
    {
        public TimberPiece(WoodSpecies species, BoardDimensions dimensions)
        {
            Species = species ?? throw new ArgumentNullException(nameof(species));
            Dimensions = dimensions;
        }

        public WoodSpecies Species { get; }

        public BoardDimensions Dimensions { get; }

        public double MassKg => Dimensions.VolumeCubicMeters * Species.DensityKgPerCubicMeter;

        /// <summary>e.g. "Spruce 2400 × 95 × 45 mm".</summary>
        public override string ToString() => $"{Species.DisplayName} {Dimensions}";
    }
}
