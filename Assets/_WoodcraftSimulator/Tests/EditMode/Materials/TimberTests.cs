using System;
using NUnit.Framework;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Materials
{
    public sealed class TimberTests
    {
        private static WoodSpecies Spruce => new WoodSpecies("spruce", "Spruce", 450);

        [Test]
        public void BoardDimensions_Volume_IsProductInCubicMeters()
        {
            BoardDimensions dimensions = BoardDimensions.FromMillimeters(2400, 95, 45);

            // 2.4 m × 0.095 m × 0.045 m
            Assert.That(dimensions.VolumeCubicMeters, Is.EqualTo(0.01026).Within(1e-12));
        }

        [TestCase(0, 95, 45)]
        [TestCase(2400, 0, 45)]
        [TestCase(2400, 95, -1)]
        public void BoardDimensions_NonPositive_Throws(double length, double width, double thickness)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BoardDimensions.FromMillimeters(length, width, thickness));
        }

        [Test]
        public void BoardDimensions_ToString_ListsLengthWidthThickness()
        {
            Assert.That(BoardDimensions.FromMillimeters(2400, 95, 45).ToString(), Is.EqualTo("2400 × 95 × 45 mm"));
            Assert.That(BoardDimensions.FromMillimeters(1000, 200, 18.5).ToString(), Is.EqualTo("1000 × 200 × 18.5 mm"));
        }

        [Test]
        public void BoardDimensions_EqualityIsByValue()
        {
            Assert.That(BoardDimensions.FromMillimeters(600, 120, 22),
                Is.EqualTo(new BoardDimensions(Length.FromMillimeters(600), Length.FromMillimeters(120), Length.FromMillimeters(22))));
        }

        [Test]
        public void TimberPiece_Mass_IsVolumeTimesDensity()
        {
            var piece = new TimberPiece(Spruce, BoardDimensions.FromMillimeters(2400, 95, 45));

            // 0.01026 m³ × 450 kg/m³
            Assert.That(piece.MassKg, Is.EqualTo(4.617).Within(1e-9));
        }

        [Test]
        public void TimberPiece_ToString_CombinesSpeciesAndDimensions()
        {
            var piece = new TimberPiece(Spruce, BoardDimensions.FromMillimeters(2400, 95, 45));

            Assert.That(piece.ToString(), Is.EqualTo("Spruce 2400 × 95 × 45 mm"));
        }

        [Test]
        public void TimberPiece_WithoutSpecies_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TimberPiece(null, BoardDimensions.FromMillimeters(1, 1, 1)));
        }

        [TestCase("", 450)]
        [TestCase("spruce", 0)]
        [TestCase("spruce", -10)]
        public void WoodSpecies_InvalidData_Throws(string id, double density)
        {
            Assert.Catch<ArgumentException>(() => new WoodSpecies(id, "Spruce", density));
        }

        [Test]
        public void WoodSpecies_MissingDisplayName_FallsBackToId()
        {
            Assert.That(new WoodSpecies("larch", "", 590).DisplayName, Is.EqualTo("larch"));
        }
    }
}
