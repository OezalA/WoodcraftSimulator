using System;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Materials
{
    /// <summary>
    /// Actual (not nominal) dimensions of a rectangular piece of timber.
    /// Convention for the 3D representation: Length along local Z, Width along local X, Thickness along local Y
    /// (a board lying flat). All three must be positive. Note: <c>default(BoardDimensions)</c> bypasses validation.
    /// </summary>
    public readonly struct BoardDimensions : IEquatable<BoardDimensions>
    {
        public BoardDimensions(Length length, Length width, Length thickness)
        {
            RequirePositive(length, nameof(length));
            RequirePositive(width, nameof(width));
            RequirePositive(thickness, nameof(thickness));

            Length = length;
            Width = width;
            Thickness = thickness;
        }

        public Length Length { get; }

        public Length Width { get; }

        public Length Thickness { get; }

        /// <summary>Volume in cubic meters (double: only used for derived values like mass and cost).</summary>
        public double VolumeCubicMeters => Length.Meters * Width.Meters * Thickness.Meters;

        public static BoardDimensions FromMillimeters(double length, double width, double thickness) =>
            new BoardDimensions(
                Measurement.Length.FromMillimeters(length),
                Measurement.Length.FromMillimeters(width),
                Measurement.Length.FromMillimeters(thickness));

        public bool Equals(BoardDimensions other) =>
            Length == other.Length && Width == other.Width && Thickness == other.Thickness;

        public override bool Equals(object obj) => obj is BoardDimensions other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Length.GetHashCode();
                hash = (hash * 397) ^ Width.GetHashCode();
                return (hash * 397) ^ Thickness.GetHashCode();
            }
        }

        public static bool operator ==(BoardDimensions a, BoardDimensions b) => a.Equals(b);

        public static bool operator !=(BoardDimensions a, BoardDimensions b) => !a.Equals(b);

        /// <summary>Length × width × thickness, e.g. "2400 × 95 × 45 mm".</summary>
        public override string ToString() =>
            $"{Length.FormatMillimeters()} × {Width.FormatMillimeters()} × {Thickness.FormatMillimeters()} mm";

        private static void RequirePositive(Length value, string parameterName)
        {
            if (value <= Measurement.Length.Zero)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Board dimensions must be positive.");
            }
        }
    }
}
