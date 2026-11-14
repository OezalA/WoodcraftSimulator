using System;
using NUnit.Framework;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Cutting
{
    public sealed class CrosscutTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly Saw HandSaw = new Saw("hand-saw", "Hand saw", Length.FromMillimeters(2));

        private static Length Mm(double value) => Length.FromMillimeters(value);

        private static TimberPiece Board(double lengthMm = 2400) =>
            new TimberPiece(Spruce, BoardDimensions.FromMillimeters(lengthMm, 95, 45));

        /// <summary>A board with one mark; returns the board, its marks and that mark.</summary>
        private static (TimberPiece piece, BoardMarks marks, PencilMark mark) Marked(double positionFromStart,
            BoardEnd measuredFrom, double lengthMm = 2400)
        {
            TimberPiece piece = Board(lengthMm);
            var marks = new BoardMarks(piece.Dimensions.Length);
            var mark = new PencilMark(Mm(positionFromStart), measuredFrom);
            marks.Add(mark);
            return (piece, marks, mark);
        }

        [Test]
        public void MeasuredFromStart_KerfGoesBeyondTheLine_MeasuredPieceKeepsItsLength()
        {
            var (piece, marks, mark) = Marked(900, BoardEnd.Start);

            Assert.That(Crosscut.TryCut(piece, marks, mark, HandSaw, out CrosscutResult result), Is.EqualTo(CrosscutRefusal.None));

            Assert.That(result.StartPiece.Dimensions.Length, Is.EqualTo(Mm(900)));
            Assert.That(result.EndPiece.Dimensions.Length, Is.EqualTo(Mm(1498)));
            Assert.That(result.KerfStartFromStart, Is.EqualTo(Mm(900)));
            Assert.That(result.KerfEndFromStart, Is.EqualTo(Mm(902)));
            Assert.That(result.MeasuredPiece, Is.SameAs(result.StartPiece));
        }

        [Test]
        public void MeasuredFromEnd_KerfGoesOnTheStartSide_MeasuredPieceKeepsItsLength()
        {
            // 900 mm from the End end = 1500 mm from the Start end.
            var (piece, marks, mark) = Marked(1500, BoardEnd.End);

            Crosscut.TryCut(piece, marks, mark, HandSaw, out CrosscutResult result);

            Assert.That(result.EndPiece.Dimensions.Length, Is.EqualTo(Mm(900)));
            Assert.That(result.StartPiece.Dimensions.Length, Is.EqualTo(Mm(1498)));
            Assert.That(result.KerfStartFromStart, Is.EqualTo(Mm(1498)));
            Assert.That(result.KerfEndFromStart, Is.EqualTo(Mm(1500)));
            Assert.That(result.MeasuredPiece, Is.SameAs(result.EndPiece));
            Assert.That(result.OtherPiece, Is.SameAs(result.StartPiece));
        }

        [TestCase(900.0, BoardEnd.Start)]
        [TestCase(1234.5, BoardEnd.End)]
        [TestCase(5.0, BoardEnd.Start)]
        public void PiecesPlusKerf_EqualTheOriginalLength_Exactly(double position, BoardEnd measuredFrom)
        {
            var (piece, marks, mark) = Marked(position, measuredFrom);

            Crosscut.TryCut(piece, marks, mark, HandSaw, out CrosscutResult result);

            Length total = result.StartPiece.Dimensions.Length + HandSaw.Kerf + result.EndPiece.Dimensions.Length;
            Assert.That(total, Is.EqualTo(piece.Dimensions.Length));
        }

        [Test]
        public void Pieces_KeepSpeciesWidthAndThickness()
        {
            var (piece, marks, mark) = Marked(900, BoardEnd.Start);

            Crosscut.TryCut(piece, marks, mark, HandSaw, out CrosscutResult result);

            foreach (TimberPiece part in new[] { result.StartPiece, result.EndPiece })
            {
                Assert.That(part.Species, Is.SameAs(Spruce));
                Assert.That(part.Dimensions.Width, Is.EqualTo(Mm(95)));
                Assert.That(part.Dimensions.Thickness, Is.EqualTo(Mm(45)));
            }
        }

        [Test]
        public void Waste_IsKerfTimesCrossSection()
        {
            var (piece, marks, mark) = Marked(900, BoardEnd.Start);

            Crosscut.TryCut(piece, marks, mark, HandSaw, out CrosscutResult result);

            // 0.002 m × 0.095 m × 0.045 m = 8.55e-6 m³ (8.55 cm³); × 450 kg/m³ = 3.85 g
            Assert.That(result.Waste.Kerf, Is.EqualTo(Mm(2)));
            Assert.That(result.Waste.VolumeCubicMeters, Is.EqualTo(8.55e-6).Within(1e-12));
            Assert.That(result.Waste.MassKg, Is.EqualTo(8.55e-6 * 450).Within(1e-12));
        }

        [Test]
        public void Mass_IsConserved_PiecesPlusWasteEqualOriginal()
        {
            var (piece, marks, mark) = Marked(900, BoardEnd.Start);

            Crosscut.TryCut(piece, marks, mark, HandSaw, out CrosscutResult result);

            double total = result.StartPiece.MassKg + result.EndPiece.MassKg + result.Waste.MassKg;
            Assert.That(total, Is.EqualTo(piece.MassKg).Within(1e-9));
        }

        [Test]
        public void OtherMarks_GoToTheirPiece_ShiftedOnTheEndPiece_AndTheCutLineIsSawnAway()
        {
            TimberPiece piece = Board();
            var marks = new BoardMarks(piece.Dimensions.Length);
            var before = new PencilMark(Mm(300), BoardEnd.Start);
            var cut = new PencilMark(Mm(900), BoardEnd.Start);
            var insideKerf = new PencilMark(Mm(901), BoardEnd.Start);
            var after = new PencilMark(Mm(1500), BoardEnd.End, BoardFace.Bottom);
            marks.Add(before);
            marks.Add(cut);
            marks.Add(insideKerf);
            marks.Add(after);

            Crosscut.TryCut(piece, marks, cut, HandSaw, out CrosscutResult result);

            Assert.That(result.StartMarks.Count, Is.EqualTo(1));
            Assert.That(result.StartMarks.All[0].PositionFromStart, Is.EqualTo(Mm(300)));

            Assert.That(result.EndMarks.Count, Is.EqualTo(1));
            Assert.That(result.EndMarks.All[0].PositionFromStart, Is.EqualTo(Mm(1500 - 902)));
            Assert.That(result.EndMarks.All[0].MeasuredFrom, Is.EqualTo(BoardEnd.End));
            Assert.That(result.EndMarks.All[0].Face, Is.EqualTo(BoardFace.Bottom));
            // Still 900 mm from the (unchanged) End end of the end piece.
            Assert.That(result.EndMarks.All[0].DistanceFromReference(result.EndPiece.Dimensions.Length), Is.EqualTo(Mm(900)));
        }

        [Test]
        public void MarksAtTheVeryEnds_StayOnTheirPieces()
        {
            TimberPiece piece = Board();
            var marks = new BoardMarks(piece.Dimensions.Length);
            var atStart = new PencilMark(Mm(0), BoardEnd.Start);
            var cut = new PencilMark(Mm(900), BoardEnd.Start);
            var atEnd = new PencilMark(Mm(2400), BoardEnd.Start);
            marks.Add(atStart);
            marks.Add(cut);
            marks.Add(atEnd);

            Crosscut.TryCut(piece, marks, cut, HandSaw, out CrosscutResult result);

            Assert.That(result.StartMarks.All[0].PositionFromStart, Is.EqualTo(Mm(0)));
            // The end piece is 1498 mm long; the mark keeps lying exactly on its far end.
            Assert.That(result.EndMarks.All[0].PositionFromStart, Is.EqualTo(result.EndPiece.Dimensions.Length));
        }

        [TestCase(902.0, 0)]   // exactly on the far edge of the kerf: sawn away
        [TestCase(902.1, 1)]   // 0.1 mm beyond: stays on the end piece, at 0.1 mm
        public void MarkAtTheKerfEdge_IsSawnAwayOnlyInsideTheKerf(double position, int expectedOnEndPiece)
        {
            TimberPiece piece = Board();
            var marks = new BoardMarks(piece.Dimensions.Length);
            var cut = new PencilMark(Mm(900), BoardEnd.Start);
            marks.Add(cut);
            marks.Add(new PencilMark(Mm(position), BoardEnd.Start));

            Crosscut.TryCut(piece, marks, cut, HandSaw, out CrosscutResult result);

            Assert.That(result.EndMarks.Count, Is.EqualTo(expectedOnEndPiece));
        }

        [Test]
        public void CuttingInTheMiddle_GivesTwoPiecesOneKerfApart()
        {
            var (piece, marks, mark) = Marked(1200, BoardEnd.Start);

            Crosscut.TryCut(piece, marks, mark, HandSaw, out CrosscutResult result);

            Assert.That(result.StartPiece.Dimensions.Length, Is.EqualTo(Mm(1200)));
            Assert.That(result.EndPiece.Dimensions.Length, Is.EqualTo(Mm(1198)));
            Assert.That(result.KerfEndFromStart - result.KerfStartFromStart, Is.EqualTo(HandSaw.Kerf));
        }

        [Test]
        public void RepeatedCuts_OnTheRemainder_StayExact()
        {
            // Cut three 600 mm pieces off a 2400 mm board, always measuring from the Start end of the remainder.
            TimberPiece remainder = Board();
            for (int i = 0; i < 3; i++)
            {
                var marks = new BoardMarks(remainder.Dimensions.Length);
                var mark = new PencilMark(Mm(600), BoardEnd.Start);
                marks.Add(mark);

                Assert.That(Crosscut.TryCut(remainder, marks, mark, HandSaw, out CrosscutResult result), Is.EqualTo(CrosscutRefusal.None));
                Assert.That(result.StartPiece.Dimensions.Length, Is.EqualTo(Mm(600)));
                remainder = result.EndPiece;
            }

            // 2400 − 3 × (600 + 2)
            Assert.That(remainder.Dimensions.Length, Is.EqualTo(Mm(594)));
        }

        [TestCase(4.9, BoardEnd.Start)]     // start piece 4.9 mm < 5 mm
        [TestCase(2393.1, BoardEnd.Start)]  // end piece 2400 − 2395.1 = 4.9 mm
        [TestCase(2395.1, BoardEnd.End)]    // end piece 4.9 mm (measured piece itself too short)
        [TestCase(6.9, BoardEnd.End)]       // start piece 6.9 − 2 = 4.9 mm
        public void TooShortPiece_IsRefused(double position, BoardEnd measuredFrom)
        {
            var (piece, marks, mark) = Marked(position, measuredFrom);

            Assert.That(Crosscut.TryCut(piece, marks, mark, HandSaw, out CrosscutResult result), Is.EqualTo(CrosscutRefusal.PieceTooShort));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ExactlyMinimumLength_IsAllowed()
        {
            var (piece, marks, mark) = Marked(5, BoardEnd.Start);

            Assert.That(Crosscut.TryCut(piece, marks, mark, HandSaw, out CrosscutResult result), Is.EqualTo(CrosscutRefusal.None));
            Assert.That(result.StartPiece.Dimensions.Length, Is.EqualTo(Mm(5)));
        }

        [Test]
        public void MarkFromAnotherBoard_IsRefused()
        {
            var (piece, marks, _) = Marked(900, BoardEnd.Start);
            var foreign = new PencilMark(Mm(900), BoardEnd.Start); // same position, but not on this board

            Assert.That(Crosscut.TryCut(piece, marks, foreign, HandSaw, out _), Is.EqualTo(CrosscutRefusal.MarkNotOnPiece));
        }

        [Test]
        public void MarksOfADifferentLength_Throw()
        {
            TimberPiece piece = Board();
            var marks = new BoardMarks(Mm(1000));
            var mark = new PencilMark(Mm(500), BoardEnd.Start);
            marks.Add(mark);

            Assert.Throws<ArgumentException>(() => Crosscut.TryCut(piece, marks, mark, HandSaw, out _));
        }

        [Test]
        public void Saw_NonPositiveKerf_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Saw("hand-saw", "Hand saw", Length.Zero));
        }

        [Test]
        public void WasteLog_SumsCuts()
        {
            var log = new MaterialWasteLog();
            int changes = 0;
            log.Changed += () => changes++;

            log.Record(new CutWaste(Mm(2), 8.55e-6, 0.0038475));
            log.Record(new CutWaste(Mm(2), 8.55e-6, 0.0038475));

            Assert.That(log.CutCount, Is.EqualTo(2));
            Assert.That(log.TotalVolumeCubicMeters, Is.EqualTo(1.71e-5).Within(1e-12));
            Assert.That(log.TotalMassKg, Is.EqualTo(0.007695).Within(1e-12));
            Assert.That(changes, Is.EqualTo(2));
        }
    }
}
