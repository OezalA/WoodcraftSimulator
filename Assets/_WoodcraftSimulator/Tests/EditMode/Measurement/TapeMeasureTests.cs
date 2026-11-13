using System;
using NUnit.Framework;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Measurement
{
    public sealed class TapeMeasureTests
    {
        private static readonly Length Board = Length.FromMillimeters(2400);
        private static readonly Length OneMillimeter = Length.FromMillimeters(1);

        private static Length Mm(double value) => Length.FromMillimeters(value);

        [TestCase(623.4, 623)]
        [TestCase(623.5, 624)]
        [TestCase(623.6, 624)]
        [TestCase(0.4, 0)]
        public void Read_FromStart_RoundsToGraduation(double pointFromStart, double expected)
        {
            Assert.That(TapeMeasure.Read(Board, Mm(pointFromStart), BoardEnd.Start, OneMillimeter), Is.EqualTo(Mm(expected)));
        }

        [Test]
        public void Read_FromEnd_MeasuresFromTheOtherEnd()
        {
            // 623.4 mm from the start is 1776.6 mm from the end → tape shows 1777 mm.
            Assert.That(TapeMeasure.Read(Board, Mm(623.4), BoardEnd.End, OneMillimeter), Is.EqualTo(Mm(1777)));
        }

        [TestCase(-50.0, BoardEnd.Start, 0)]
        [TestCase(2500.0, BoardEnd.Start, 2400)]
        [TestCase(-50.0, BoardEnd.End, 2400)]
        [TestCase(2500.0, BoardEnd.End, 0)]
        public void Read_BeyondThePiece_IsClampedToItsEnds(double pointFromStart, BoardEnd hook, double expected)
        {
            Assert.That(TapeMeasure.Read(Board, Mm(pointFromStart), hook, OneMillimeter), Is.EqualTo(Mm(expected)));
        }

        [Test]
        public void DistanceFromHook_KeepsTenthOfMillimeterPrecision()
        {
            Assert.That(TapeMeasure.DistanceFromHook(Board, Mm(899.7), BoardEnd.Start), Is.EqualTo(Mm(899.7)));
        }

        [Test]
        public void Read_WithCoarserGraduation_RoundsToThatStep()
        {
            Assert.That(TapeMeasure.Read(Board, Mm(623.4), BoardEnd.Start, Mm(5)), Is.EqualTo(Mm(625)));
        }

        [TestCase(-300.0, BoardEnd.Start)]
        [TestCase(1000.0, BoardEnd.Start)]
        [TestCase(1200.0, BoardEnd.Start)]
        [TestCase(1200.1, BoardEnd.End)]
        [TestCase(2700.0, BoardEnd.End)]
        public void NearestEnd_PicksTheCloserEnd(double positionFromStart, BoardEnd expected)
        {
            Assert.That(TapeMeasure.NearestEnd(Board, Mm(positionFromStart)), Is.EqualTo(expected));
        }

        [Test]
        public void NonPositivePieceLength_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TapeMeasure.Read(Length.Zero, Mm(1), BoardEnd.Start, OneMillimeter));
            Assert.Throws<ArgumentOutOfRangeException>(() => TapeMeasure.NearestEnd(Mm(-1), Mm(1)));
        }

        [TestCase(2.5, 1.0, 3.0)]
        [TestCase(2.4, 1.0, 2.0)]
        [TestCase(-2.5, 1.0, -3.0)]
        [TestCase(1497.5, 1.0, 1498.0)]
        [TestCase(1497.5, 5.0, 1500.0)]
        public void Length_RoundTo_RoundsHalfAwayFromZero(double value, double step, double expected)
        {
            Assert.That(Mm(value).RoundTo(Mm(step)), Is.EqualTo(Mm(expected)));
        }

        [Test]
        public void Length_RoundTo_NonPositiveStep_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Mm(10).RoundTo(Length.Zero));
        }

        [Test]
        public void Length_Clamp_LimitsToRange()
        {
            Assert.That(Length.Clamp(Mm(-1), Length.Zero, Board), Is.EqualTo(Length.Zero));
            Assert.That(Length.Clamp(Mm(3000), Length.Zero, Board), Is.EqualTo(Board));
            Assert.That(Length.Clamp(Mm(900), Length.Zero, Board), Is.EqualTo(Mm(900)));
        }
    }
}
