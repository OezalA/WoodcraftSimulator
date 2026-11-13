using System;

namespace WoodcraftSimulator.Materials
{
    /// <summary>
    /// Immutable material data of a wood species, as used by game rules.
    /// Created from a WoodSpeciesDefinition asset (static configuration) at runtime.
    /// </summary>
    public sealed class WoodSpecies
    {
        public WoodSpecies(string id, string displayName, double densityKgPerCubicMeter)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Species id must not be empty.", nameof(id));
            }

            if (double.IsNaN(densityKgPerCubicMeter) || densityKgPerCubicMeter <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(densityKgPerCubicMeter), densityKgPerCubicMeter,
                    "Density must be positive.");
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            DensityKgPerCubicMeter = densityKgPerCubicMeter;
        }

        /// <summary>Stable identifier used by data and saves, e.g. "spruce".</summary>
        public string Id { get; }

        public string DisplayName { get; }

        /// <summary>Typical density at ~12 % moisture content (air-dry), kg/m³.</summary>
        public double DensityKgPerCubicMeter { get; }

        public override string ToString() => DisplayName;
    }
}
