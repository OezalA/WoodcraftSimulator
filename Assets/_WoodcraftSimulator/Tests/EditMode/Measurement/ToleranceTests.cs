using System;
using NUnit.Framework;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Measurement
{
    public sealed class ToleranceTests
    {
        private static Length Mm(double value) => Length.FromMillimeters(value);

        [TestCase(900.0, 0)]
        [TestCase(899.6, -0.4)]
        [TestCase(901.2, 1.2)]
        public void Check_DeviationIsActualMinusTarget(double actual, double expectedDeviation)
        {
            ToleranceCheck check = Tolerance.Default.Check(Mm(900), Mm(actual));
            Assert.That(check.Deviation, Is.EqualTo(Mm(expectedDeviation)));
        }

        [TestCase(900.0, true)]
        [TestCase(899.0, true)]   // exactly at the limit passes
        [TestCase(901.0, true)]
        [TestCase(898.9, false)]  // 0.1 mm beyond the limit fails
        [TestCase(901.1, false)]
        public void Check_DefaultTolerance_IsPlusMinusOneMillimeter(double actual, bool expectedWithin)
        {
            Assert.That(Tolerance.Default.Check(Mm(900), Mm(actual)).IsWithin, Is.EqualTo(expectedWithin));
        }

        [Test]
        public void Check_ZeroTolerance_OnlyExactPasses()
        {
            var exact = new Tolerance(Length.Zero);
            Assert.That(exact.Check(Mm(900), Mm(900)).IsWithin, Is.True);
            Assert.That(exact.Check(Mm(900), Mm(900.1)).IsWithin, Is.False);
        }

        [Test]
        public void Constructor_NegativeTolerance_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tolerance(Mm(-0.1)));
        }

        [Test]
        public void ToString_ShowsPlusMinus()
        {
            Assert.That(new Tolerance(Mm(0.5)).ToString(), Is.EqualTo("±0.5 mm"));
        }
    }
}
