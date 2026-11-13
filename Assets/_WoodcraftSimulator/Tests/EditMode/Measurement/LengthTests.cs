using System;
using NUnit.Framework;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Measurement
{
    public sealed class LengthTests
    {
        [Test]
        public void FromMillimeters_Integer_IsExact()
        {
            Assert.That(Length.FromMillimeters(2400).TenthsOfMillimeter, Is.EqualTo(24000));
        }

        [TestCase(2.5, 25)]
        [TestCase(2.54, 25)]
        [TestCase(2.56, 26)]
        [TestCase(-1.26, -13)]
        [TestCase(0.04, 0)]
        public void FromMillimeters_Double_RoundsToTenthOfMillimeter(double millimeters, long expectedTenths)
        {
            Assert.That(Length.FromMillimeters(millimeters).TenthsOfMillimeter, Is.EqualTo(expectedTenths));
        }

        [Test]
        public void FromMeters_EqualsSameValueInMillimeters()
        {
            Assert.That(Length.FromMeters(2.4), Is.EqualTo(Length.FromMillimeters(2400)));
        }

        [Test]
        public void Conversions_ReturnExpectedUnits()
        {
            Length length = Length.FromMillimeters(2400);

            Assert.That(length.Millimeters, Is.EqualTo(2400.0));
            Assert.That(length.Centimeters, Is.EqualTo(240.0));
            Assert.That(length.Meters, Is.EqualTo(2.4).Within(1e-12));
        }

        [Test]
        public void Arithmetic_AddsAndSubtractsExactly()
        {
            Length board = Length.FromMillimeters(2400);
            Length piece = Length.FromMillimeters(900);
            Length kerf = Length.FromMillimeters(2.5);

            Length remainder = board - piece - kerf;

            Assert.That(remainder, Is.EqualTo(Length.FromMillimeters(1497.5)));
        }

        [Test]
        public void RepeatedAddition_DoesNotDrift()
        {
            // 10 000 × 0.1 mm must be exactly 1000 mm (a float in meters would accumulate error here).
            Length total = Length.Zero;
            Length step = Length.FromTenthsOfMillimeter(1);

            for (int i = 0; i < 10000; i++)
            {
                total += step;
            }

            Assert.That(total, Is.EqualTo(Length.FromMillimeters(1000)));
        }

        [Test]
        public void Comparison_OrdersByValue()
        {
            Length shorter = Length.FromMillimeters(899.9);
            Length longer = Length.FromMillimeters(900);

            Assert.That(shorter < longer, Is.True);
            Assert.That(longer >= shorter, Is.True);
            Assert.That(shorter.CompareTo(longer), Is.LessThan(0));
            Assert.That((shorter - longer).Abs(), Is.EqualTo(Length.FromTenthsOfMillimeter(1)));
        }

        [TestCase(2400.0, "2400 mm")]
        [TestCase(2.5, "2.5 mm")]
        [TestCase(0.0, "0 mm")]
        [TestCase(-12.3, "-12.3 mm")]
        public void ToString_UsesMillimetersWithOneDecimal(double millimeters, string expected)
        {
            Assert.That(Length.FromMillimeters(millimeters).ToString(), Is.EqualTo(expected));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void FromMillimeters_NonFinite_Throws(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Length.FromMillimeters(value));
        }
    }
}
