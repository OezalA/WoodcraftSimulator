using System;
using System.Globalization;

namespace WoodcraftSimulator.Measurement
{
    /// <summary>
    /// A length with a fixed resolution of 0.1 mm, stored as an integer count of tenths of a millimeter.
    /// Integer storage keeps repeated additions/subtractions (successive cuts, kerf losses) exact,
    /// unlike float meters. Convert to meters only at the presentation boundary (Unity world space).
    /// May be negative (e.g. the difference between two lengths).
    /// </summary>
    public readonly struct Length : IEquatable<Length>, IComparable<Length>
    {
        public const long UnitsPerMillimeter = 10;
        private const double UnitsPerMeter = 10000.0;

        private readonly long units;

        private Length(long units) => this.units = units;

        public static Length Zero => default;

        /// <summary>Exact value in tenths of a millimeter.</summary>
        public long TenthsOfMillimeter => units;

        public double Millimeters => units / (double)UnitsPerMillimeter;

        public double Centimeters => units / (UnitsPerMillimeter * 10.0);

        public double Meters => units / UnitsPerMeter;

        public static Length FromTenthsOfMillimeter(long tenths) => new Length(tenths);

        public static Length FromMillimeters(long millimeters) => new Length(checked(millimeters * UnitsPerMillimeter));

        /// <summary>Rounds to the nearest 0.1 mm (midpoints away from zero).</summary>
        public static Length FromMillimeters(double millimeters) => FromScaled(millimeters, UnitsPerMillimeter, nameof(millimeters));

        /// <summary>Rounds to the nearest 0.1 mm (midpoints away from zero).</summary>
        public static Length FromMeters(double meters) => FromScaled(meters, UnitsPerMeter, nameof(meters));

        public Length Abs() => new Length(Math.Abs(units));

        /// <summary>
        /// Rounds to the nearest multiple of <paramref name="step"/> (midpoints away from zero),
        /// e.g. a tape measure graduated in 1 mm reads 623.5 mm as 624 mm.
        /// </summary>
        public Length RoundTo(Length step)
        {
            if (step.units <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(step), step, "Rounding step must be positive.");
            }

            long quotient = units / step.units;
            long remainder = units % step.units; // same sign as units
            if (Math.Abs(remainder) * 2 >= step.units)
            {
                quotient += Math.Sign(units);
            }

            return new Length(checked(quotient * step.units));
        }

        public static Length Min(Length a, Length b) => a <= b ? a : b;

        public static Length Max(Length a, Length b) => a >= b ? a : b;

        public static Length Clamp(Length value, Length min, Length max) => Max(min, Min(value, max));

        public static Length operator +(Length a, Length b) => new Length(checked(a.units + b.units));

        public static Length operator -(Length a, Length b) => new Length(checked(a.units - b.units));

        public static Length operator -(Length a) => new Length(checked(-a.units));

        public static Length operator *(Length a, long factor) => new Length(checked(a.units * factor));

        public static Length operator *(long factor, Length a) => a * factor;

        public static bool operator ==(Length a, Length b) => a.units == b.units;

        public static bool operator !=(Length a, Length b) => a.units != b.units;

        public static bool operator <(Length a, Length b) => a.units < b.units;

        public static bool operator >(Length a, Length b) => a.units > b.units;

        public static bool operator <=(Length a, Length b) => a.units <= b.units;

        public static bool operator >=(Length a, Length b) => a.units >= b.units;

        public bool Equals(Length other) => units == other.units;

        public override bool Equals(object obj) => obj is Length other && Equals(other);

        public override int GetHashCode() => units.GetHashCode();

        public int CompareTo(Length other) => units.CompareTo(other.units);

        /// <summary>Millimeters with up to one decimal, invariant culture, e.g. "2400 mm", "2.5 mm".</summary>
        public override string ToString() => FormatMillimeters() + " mm";

        /// <summary>Numeric part of <see cref="ToString"/> without unit, e.g. "2400" or "2.5".</summary>
        public string FormatMillimeters() => Millimeters.ToString("0.#", CultureInfo.InvariantCulture);

        private static Length FromScaled(double value, double unitsPerInput, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Length must be a finite number.");
            }

            double scaled = Math.Round(value * unitsPerInput, MidpointRounding.AwayFromZero);
            if (scaled > long.MaxValue || scaled < long.MinValue)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Length is out of range.");
            }

            return new Length((long)scaled);
        }
    }
}
