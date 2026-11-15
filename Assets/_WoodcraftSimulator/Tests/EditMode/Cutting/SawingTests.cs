using System;
using NUnit.Framework;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Cutting
{
    public sealed class SawingTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly Saw HandSaw = new Saw("hand-saw", "Hand saw", Length.FromMillimeters(2));

        private static Length Mm(double value) => Length.FromMillimeters(value);

        private static TimberPiece Board(double lengthMm) =>
            new TimberPiece(Spruce, BoardDimensions.FromMillimeters(lengthMm, 145, 18));

        [Test]
        public void TryCutAt_KerfCenteredOnTheBlade_WhereverTheLineIs()
        {
            TimberPiece piece = Board(2000);
            var marks = new BoardMarks(piece.Dimensions.Length);
            marks.Add(new PencilMark(Mm(600), BoardEnd.Start));

            // Blade 1.5 mm past the line on the waste side: the measured piece is 0.5 mm too long.
            Assert.That(Crosscut.TryCutAt(piece, marks, Mm(601.5), BoardEnd.Start, HandSaw, out CrosscutResult result),
                Is.EqualTo(CrosscutRefusal.None));
            Assert.That(result.KerfStartFromStart, Is.EqualTo(Mm(600.5)));
            Assert.That(result.MeasuredPiece.Dimensions.Length, Is.EqualTo(Mm(600.5)));
            Assert.That(result.OtherPiece.Dimensions.Length, Is.EqualTo(Mm(1397.5)));
            Assert.That(result.StartMarks.Count, Is.EqualTo(1)); // the line stays on the piece (600 < 600.5)
        }

        [Test]
        public void TryCutAt_BladeOnTheLine_SawsTheLineAway_PieceShortByHalfAKerf()
        {
            TimberPiece piece = Board(2000);
            var marks = new BoardMarks(piece.Dimensions.Length);
            marks.Add(new PencilMark(Mm(600), BoardEnd.Start));

            Crosscut.TryCutAt(piece, marks, Mm(600), BoardEnd.Start, HandSaw, out CrosscutResult result);

            Assert.That(result.MeasuredPiece.Dimensions.Length, Is.EqualTo(Mm(599)));
            Assert.That(result.StartMarks.Count + result.EndMarks.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryCutAt_MeasuredFromTheEnd()
        {
            TimberPiece piece = Board(1000);
            var marks = new BoardMarks(piece.Dimensions.Length);

            Crosscut.TryCutAt(piece, marks, Mm(399), BoardEnd.End, HandSaw, out CrosscutResult result);

            Assert.That(result.MeasuredSide, Is.EqualTo(BoardEnd.End));
            Assert.That(result.MeasuredPiece.Dimensions.Length, Is.EqualTo(Mm(600)));
        }

        [TestCase(3.0)]
        [TestCase(998.0)]
        [TestCase(-10.0)]
        public void TryCutAt_TooCloseToAnEnd_IsRefused(double bladeMm)
        {
            TimberPiece piece = Board(1000);
            Assert.That(Crosscut.TryCutAt(piece, new BoardMarks(piece.Dimensions.Length), Mm(bladeMm), BoardEnd.Start,
                HandSaw, out _), Is.EqualTo(CrosscutRefusal.PieceTooShort));
        }

        // --- strokes ---

        private static SawingProcess Process() =>
            new SawingProcess(crossSectionSquareCentimeters: 26.1, densityKgPerCubicMeter: 450.0);

        /// <summary>Full back-and-forth strokes of the given length at the given speed, with sideways input per frame.</summary>
        private static void Strokes(SawingProcess process, int count, double lengthMm, double speedMmPerSecond,
            double sidewaysPerFrame = 0.0)
        {
            const double frame = 0.02;
            double step = speedMmPerSecond * frame;
            for (int stroke = 0; stroke < count && !process.IsDone; stroke++)
            {
                double direction = stroke % 2 == 0 ? 1.0 : -1.0;
                for (double moved = 0.0; moved < lengthMm && !process.IsDone; moved += step)
                {
                    process.Feed(direction * step, sidewaysPerFrame, frame);
                }
            }
        }

        [Test]
        public void LongSteadyStrokes_CutThrough_WithoutDrift()
        {
            SawingProcess process = Process();
            // 26.1 cm² × 120 mm = 3132 mm of saw travel (start at half efficiency adds a little).
            Assert.That(process.RequiredStrokeMillimeters, Is.EqualTo(3132.0).Within(1e-6));

            Strokes(process, 20, 250.0, 400.0);

            Assert.That(process.IsDone, Is.True);
            Assert.That(process.DriftMillimeters, Is.EqualTo(0.0));
        }

        [Test]
        public void MovementBeyondTheBlade_DoesNotCut()
        {
            SawingProcess process = Process();
            process.Feed(400.0, 0.0, 1.0);   // clamped to +250 (half of 500 mm)
            double afterFirst = process.Progress;
            process.Feed(400.0, 0.0, 1.0);   // already at the end of the blade

            Assert.That(process.StrokePosition, Is.EqualTo(250.0));
            Assert.That(process.Progress, Is.EqualTo(afterFirst));
        }

        [Test]
        public void SidewaysHand_WhileStarting_MovesTheBlade_FastStrokesMore()
        {
            SawingProcess slow = Process();
            slow.Feed(5.0, 0.1, 0.02);          // 250 mm/s
            Assert.That(slow.DriftMillimeters, Is.EqualTo(0.1).Within(1e-9));
            Assert.That(slow.Jumped, Is.False);

            SawingProcess fast = Process();
            fast.Feed(30.0, 0.1, 0.02);         // 1500 mm/s: the blade jumps
            Assert.That(fast.DriftMillimeters, Is.EqualTo(0.4).Within(1e-9));
            Assert.That(fast.Jumped, Is.True);
        }

        [Test]
        public void OnceStarted_TheKerfGuidesTheBlade()
        {
            SawingProcess process = Process();
            Strokes(process, 4, 250.0, 400.0);
            Assert.That(process.IsStarting, Is.False);

            double drift = process.DriftMillimeters;
            process.Feed(-30.0, 5.0, 0.02);
            Assert.That(process.DriftMillimeters, Is.EqualTo(drift));
            Assert.That(process.Jumped, Is.False);
        }

        [Test]
        public void DenserWood_NeedsMoreStrokes()
        {
            Assert.That(new SawingProcess(26.1, 700.0).RequiredStrokeMillimeters,
                Is.GreaterThan(Process().RequiredStrokeMillimeters));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SawingProcess(0.0, 450.0));
        }
    }
}
