using System;
using System.Globalization;

namespace WoodcraftSimulator.Economy
{
    /// <summary>
    /// An amount of money in whole euro cents (D-040). Integer cents keep sums exact; prorated amounts (a share of a
    /// board, a few screws from a box) are rounded to the cent once, where they are calculated.
    /// </summary>
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        /// <summary>Currency symbol of all amounts (owner's choice K1). The only place it is written.</summary>
        public const string Symbol = "€";

        private Money(long cents)
        {
            Cents = cents;
        }

        public static Money Zero => default;

        public long Cents { get; }

        public bool IsNegative => Cents < 0;

        public static Money FromCents(long cents) => new Money(cents);

        public static Money FromEuros(decimal euros) =>
            new Money((long)Math.Round(euros * 100m, MidpointRounding.AwayFromZero));

        /// <summary>This amount times a factor, rounded to the cent (half away from zero).</summary>
        public Money Scale(double factor)
        {
            if (double.IsNaN(factor) || double.IsInfinity(factor))
            {
                throw new ArgumentOutOfRangeException(nameof(factor), factor, "Factor must be a finite number.");
            }

            return new Money((long)Math.Round(Cents * factor, MidpointRounding.AwayFromZero));
        }

        public static Money operator +(Money a, Money b) => new Money(a.Cents + b.Cents);

        public static Money operator -(Money a, Money b) => new Money(a.Cents - b.Cents);

        public static Money operator -(Money a) => new Money(-a.Cents);

        public static Money operator *(Money a, int factor) => new Money(a.Cents * factor);

        public static bool operator ==(Money a, Money b) => a.Cents == b.Cents;

        public static bool operator !=(Money a, Money b) => a.Cents != b.Cents;

        public static bool operator <(Money a, Money b) => a.Cents < b.Cents;

        public static bool operator >(Money a, Money b) => a.Cents > b.Cents;

        public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;

        public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;

        public bool Equals(Money other) => Cents == other.Cents;

        public override bool Equals(object obj) => obj is Money other && Equals(other);

        public override int GetHashCode() => Cents.GetHashCode();

        public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

        /// <summary>E.g. "€12.50", "-€3.05".</summary>
        public override string ToString()
        {
            long absolute = Math.Abs(Cents);
            string amount = (absolute / 100).ToString(CultureInfo.InvariantCulture) + "." +
                            (absolute % 100).ToString("00", CultureInfo.InvariantCulture);
            return (Cents < 0 ? "-" : string.Empty) + Symbol + amount;
        }
    }
}
