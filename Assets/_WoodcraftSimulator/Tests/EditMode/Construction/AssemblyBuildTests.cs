using System;
using NUnit.Framework;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Construction
{
    public sealed class AssemblyBuildTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly FastenerSpec Screw = new FastenerSpec("wood-screw-5x80", "Wood screw 5 × 80 mm",
            Length.FromMillimeters(5), Length.FromMillimeters(80));
        private static readonly FastenerSpec ShortScrew = new FastenerSpec("wood-screw-4x40", "Wood screw 4 × 40 mm",
            Length.FromMillimeters(4), Length.FromMillimeters(40));

        private static TimberPiece Piece(double lengthMm, double widthMm = 95, double thicknessMm = 45) =>
            new TimberPiece(Spruce, BoardDimensions.FromMillimeters(lengthMm, widthMm, thicknessMm));

        /// <summary>The Phase 5 frame: 600 × 300 mm outside, butt joints, 2 screws per corner.</summary>
        private static Blueprint Frame(params string[] sidePrerequisites)
        {
            BoardDimensions rail = BoardDimensions.FromMillimeters(600, 95, 45);
            BoardDimensions side = BoardDimensions.FromMillimeters(210, 95, 45);
            var slots = new[]
            {
                new PartSlot("bottom", "Bottom rail", rail),
                new PartSlot("top", "Top rail", rail),
                new PartSlot("left", "Left side", side, sidePrerequisites),
                new PartSlot("right", "Right side", side, sidePrerequisites)
            };
            var connections = new[]
            {
                new ConnectionSpec("bottom-left", "bottom", "left", Screw.Id, 2),
                new ConnectionSpec("bottom-right", "bottom", "right", Screw.Id, 2),
                new ConnectionSpec("top-left", "top", "left", Screw.Id, 2),
                new ConnectionSpec("top-right", "top", "right", Screw.Id, 2)
            };
            return new Blueprint("frame-600x300", "Frame 600 × 300 mm", slots, connections,
                new Tolerance(Length.FromMillimeters(1)), new Tolerance(Length.FromMillimeters(5)));
        }

        private static AssemblyBuild FullyPlaced()
        {
            var build = new AssemblyBuild(Frame());
            build.TryPlace("bottom", Piece(600));
            build.TryPlace("top", Piece(600));
            build.TryPlace("left", Piece(210));
            build.TryPlace("right", Piece(210));
            return build;
        }

        private static void FastenAll(AssemblyBuild build)
        {
            foreach (ConnectionSpec connection in build.Blueprint.Connections)
            {
                for (int point = 0; point < connection.FastenerCount; point++)
                {
                    Assert.That(build.TryFasten(connection.Id, point, Screw), Is.EqualTo(FastenResult.Fastened));
                }
            }
        }

        [TestCase(600.0, true, true)]
        [TestCase(600.9, true, true)]
        [TestCase(598.8, false, true)]   // 1.2 mm short: fits, but inaccurate
        [TestCase(605.0, false, true)]   // at the fit limit
        [TestCase(605.1, false, false)]  // too long: doesn't fit
        [TestCase(594.0, false, false)]
        public void CheckFit_UsesAccuracyAndFitTolerances(double lengthMm, bool accurate, bool fits)
        {
            var build = new AssemblyBuild(Frame());
            build.Blueprint.TryGetSlot("bottom", out PartSlot slot);

            SlotFit fit = build.CheckFit(slot, Piece(lengthMm));

            Assert.That(fit.IsAccurate, Is.EqualTo(accurate));
            Assert.That(fit.Fits, Is.EqualTo(fits));
        }

        [Test]
        public void CheckFit_ReportsDeviationPerDimension()
        {
            var build = new AssemblyBuild(Frame());
            build.Blueprint.TryGetSlot("left", out PartSlot slot);

            SlotFit fit = build.CheckFit(slot, Piece(225));

            Assert.That(fit.Length.Deviation, Is.EqualTo(Length.FromMillimeters(15)));
            Assert.That(fit.Width.Deviation, Is.EqualTo(Length.Zero));
        }

        [Test]
        public void CheckFit_WrongCrossSection_DoesNotFit()
        {
            var build = new AssemblyBuild(Frame());
            build.Blueprint.TryGetSlot("bottom", out PartSlot slot);

            Assert.That(build.CheckFit(slot, Piece(600, 145, 45)).Fits, Is.False);
        }

        [Test]
        public void TryPlace_FittingPiece_IsInstalled()
        {
            var build = new AssemblyBuild(Frame());
            TimberPiece piece = Piece(600);

            Assert.That(build.TryPlace("bottom", piece), Is.EqualTo(PlaceResult.Placed));
            Assert.That(build.GetPiece("bottom"), Is.SameAs(piece));
        }

        [Test]
        public void TryPlace_Refusals()
        {
            var build = new AssemblyBuild(Frame());
            build.TryPlace("bottom", Piece(600));

            Assert.That(build.TryPlace("bottom", Piece(600)), Is.EqualTo(PlaceResult.SlotFilled));
            Assert.That(build.TryPlace("shelf", Piece(600)), Is.EqualTo(PlaceResult.UnknownSlot));
            Assert.That(build.TryPlace("left", Piece(300)), Is.EqualTo(PlaceResult.DoesNotFit));
            Assert.That(build.GetPiece("left"), Is.Null);
        }

        [Test]
        public void TryPlace_PrerequisiteMissing_UntilItIsPlaced()
        {
            var build = new AssemblyBuild(Frame("bottom"));

            Assert.That(build.TryPlace("left", Piece(210)), Is.EqualTo(PlaceResult.PrerequisiteMissing));
            build.TryPlace("bottom", Piece(600));
            Assert.That(build.TryPlace("left", Piece(210)), Is.EqualTo(PlaceResult.Placed));
        }

        [Test]
        public void TryFasten_NeedsBothParts()
        {
            var build = new AssemblyBuild(Frame());
            build.TryPlace("bottom", Piece(600));

            Assert.That(build.TryFasten("bottom-left", 0, Screw), Is.EqualTo(FastenResult.PartsMissing));
            build.TryPlace("left", Piece(210));
            Assert.That(build.TryFasten("bottom-left", 0, Screw), Is.EqualTo(FastenResult.Fastened));
            Assert.That(build.IsFastened("bottom-left", 0), Is.True);
        }

        [Test]
        public void TryFasten_Refusals()
        {
            AssemblyBuild build = FullyPlaced();
            build.TryFasten("bottom-left", 0, Screw);

            Assert.That(build.TryFasten("bottom-left", 0, Screw), Is.EqualTo(FastenResult.PointAlreadyFastened));
            Assert.That(build.TryFasten("bottom-left", 2, Screw), Is.EqualTo(FastenResult.InvalidPoint));
            Assert.That(build.TryFasten("bottom-left", -1, Screw), Is.EqualTo(FastenResult.InvalidPoint));
            Assert.That(build.TryFasten("middle", 0, Screw), Is.EqualTo(FastenResult.UnknownConnection));
            Assert.That(build.TryFasten("bottom-left", 1, ShortScrew), Is.EqualTo(FastenResult.WrongFastener));
        }

        [Test]
        public void TryRemove_BeforeFastening_GivesThePieceBack()
        {
            var build = new AssemblyBuild(Frame());
            TimberPiece piece = Piece(600);
            build.TryPlace("bottom", piece);

            Assert.That(build.TryRemove("bottom", out TimberPiece removed), Is.EqualTo(RemoveResult.Removed));
            Assert.That(removed, Is.SameAs(piece));
            Assert.That(build.GetPiece("bottom"), Is.Null);
            Assert.That(build.TryRemove("bottom", out _), Is.EqualTo(RemoveResult.SlotEmpty));
        }

        [Test]
        public void CanRemove_AnswersWithoutChangingTheBuild()
        {
            AssemblyBuild build = FullyPlaced();
            build.TryFasten("bottom-left", 0, Screw);

            Assert.That(build.CanRemove("right"), Is.EqualTo(RemoveResult.Removed));
            Assert.That(build.CanRemove("bottom"), Is.EqualTo(RemoveResult.Fastened));
            Assert.That(build.CanRemove("shelf"), Is.EqualTo(RemoveResult.UnknownSlot));
            Assert.That(build.GetPiece("right"), Is.Not.Null);
        }

        [Test]
        public void TryRemove_FastenedPart_IsRefused_OtherPartsStillFree()
        {
            AssemblyBuild build = FullyPlaced();
            build.TryFasten("bottom-left", 0, Screw);

            Assert.That(build.TryRemove("bottom", out _), Is.EqualTo(RemoveResult.Fastened));
            Assert.That(build.TryRemove("left", out _), Is.EqualTo(RemoveResult.Fastened));
            Assert.That(build.TryRemove("right", out _), Is.EqualTo(RemoveResult.Removed));
        }

        [Test]
        public void Completion_RequiresAllPartsAndAllFasteners_AndIsRaisedOnce()
        {
            AssemblyBuild build = FullyPlaced();
            int completed = 0;
            build.Completed += () => completed++;

            Assert.That(build.IsComplete, Is.False);
            FastenAll(build);

            Assert.That(build.IsComplete, Is.True);
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void Report_ListsComponentsSeparately()
        {
            var build = new AssemblyBuild(Frame());
            build.TryPlace("bottom", Piece(600));
            build.TryPlace("top", Piece(602.5));  // fits, inaccurate
            build.TryPlace("left", Piece(210.4));
            build.TryFasten("bottom-left", 0, Screw);

            AssemblyQualityReport report = build.CreateReport();

            Assert.That(report.PartsRequired, Is.EqualTo(4));
            Assert.That(report.PartsInstalled, Is.EqualTo(3));
            Assert.That(report.AccurateParts, Is.EqualTo(2));
            Assert.That(report.FastenersRequired, Is.EqualTo(8));
            Assert.That(report.FastenersInstalled, Is.EqualTo(1));
            Assert.That(report.IsComplete, Is.False);
            Assert.That(report.Parts[1].Fit.Value.Length.Deviation, Is.EqualTo(Length.FromMillimeters(2.5)));
            Assert.That(report.Parts[3].IsInstalled, Is.False);
        }

        [Test]
        public void Blueprint_InvalidDefinitions_Throw()
        {
            BoardDimensions dims = BoardDimensions.FromMillimeters(600, 95, 45);
            var one = new PartSlot("a", "A", dims);
            var two = new PartSlot("b", "B", dims);
            var accuracy = new Tolerance(Length.FromMillimeters(1));
            var fit = new Tolerance(Length.FromMillimeters(5));
            var noConnections = Array.Empty<ConnectionSpec>();

            Assert.Throws<ArgumentException>(() => new Blueprint("x", "X", new[] { one, new PartSlot("a", "A2", dims) }, noConnections, accuracy, fit));
            Assert.Throws<ArgumentException>(() => new Blueprint("x", "X", Array.Empty<PartSlot>(), noConnections, accuracy, fit));
            Assert.Throws<ArgumentException>(() => new Blueprint("x", "X", new[] { one, new PartSlot("c", "C", dims, new[] { "missing" }) }, noConnections, accuracy, fit));
            Assert.Throws<ArgumentException>(() => new Blueprint("x", "X", new[] { one, two }, new[] { new ConnectionSpec("j", "a", "z", Screw.Id, 2) }, accuracy, fit));
            Assert.Throws<ArgumentException>(() => new Blueprint("x", "X", new[] { one, two }, new[] { new ConnectionSpec("j", "a", "a", Screw.Id, 2) }, accuracy, fit));
            Assert.Throws<ArgumentException>(() => new Blueprint("x", "X", new[] { one, two }, noConnections, fit, accuracy)); // fit < accuracy
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionSpec("j", "a", "b", Screw.Id, 0));
        }
    }
}
