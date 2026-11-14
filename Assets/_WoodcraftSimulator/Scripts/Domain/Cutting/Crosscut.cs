using System;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Cutting
{
    /// <summary>Why a crosscut can't be made.</summary>
    public enum CrosscutRefusal
    {
        None,

        /// <summary>The cut mark is not one of the piece's marks.</summary>
        MarkNotOnPiece,

        /// <summary>One of the resulting pieces would be shorter than the minimum piece length.</summary>
        PieceTooShort
    }

    /// <summary>Material lost in one cut (sawdust): the kerf across the full cross-section.</summary>
    public readonly struct CutWaste
    {
        public CutWaste(Length kerf, double volumeCubicMeters, double massKg)
        {
            Kerf = kerf;
            VolumeCubicMeters = volumeCubicMeters;
            MassKg = massKg;
        }

        public Length Kerf { get; }

        public double VolumeCubicMeters { get; }

        public double MassKg { get; }
    }

    /// <summary>
    /// The two pieces of a crosscut. Both keep the original's orientation: <see cref="StartPiece"/> contains the
    /// original Start end and lies at [0, <see cref="KerfStartFromStart"/>]; <see cref="EndPiece"/> contains the
    /// original End end and lies at [<see cref="KerfEndFromStart"/>, original length] (positions in the original).
    /// </summary>
    public sealed class CrosscutResult
    {
        internal CrosscutResult(TimberPiece startPiece, TimberPiece endPiece, BoardMarks startMarks, BoardMarks endMarks,
            Length kerfStartFromStart, Length kerfEndFromStart, BoardEnd measuredSide, CutWaste waste)
        {
            StartPiece = startPiece;
            EndPiece = endPiece;
            StartMarks = startMarks;
            EndMarks = endMarks;
            KerfStartFromStart = kerfStartFromStart;
            KerfEndFromStart = kerfEndFromStart;
            MeasuredSide = measuredSide;
            Waste = waste;
        }

        public TimberPiece StartPiece { get; }

        public TimberPiece EndPiece { get; }

        /// <summary>Marks that were on the start piece (positions unchanged).</summary>
        public BoardMarks StartMarks { get; }

        /// <summary>Marks that were on the end piece (positions shifted to its own Start end).</summary>
        public BoardMarks EndMarks { get; }

        public Length KerfStartFromStart { get; }

        public Length KerfEndFromStart { get; }

        /// <summary>The side the cut mark was measured from: that piece has exactly the marked length.</summary>
        public BoardEnd MeasuredSide { get; }

        public TimberPiece MeasuredPiece => MeasuredSide == BoardEnd.Start ? StartPiece : EndPiece;

        public TimberPiece OtherPiece => MeasuredSide == BoardEnd.Start ? EndPiece : StartPiece;

        public CutWaste Waste { get; }
    }

    /// <summary>
    /// A straight cut across a piece at one of its pencil marks. As a carpenter does, the kerf goes on the waste
    /// side of the line, i.e. away from the end the mark was measured from, so the measured piece keeps exactly the
    /// marked length and the other piece loses the kerf.
    /// </summary>
    public static class Crosscut
    {
        /// <summary>Shortest piece a hand cut may produce (thinner slivers break off or can't be held).</summary>
        public static Length DefaultMinimumPieceLength => Length.FromMillimeters(5);

        public static CrosscutRefusal TryCut(TimberPiece piece, BoardMarks marks, PencilMark cutMark, Saw saw,
            out CrosscutResult result) =>
            TryCut(piece, marks, cutMark, saw, DefaultMinimumPieceLength, out result);

        public static CrosscutRefusal TryCut(TimberPiece piece, BoardMarks marks, PencilMark cutMark, Saw saw,
            Length minimumPieceLength, out CrosscutResult result)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (marks == null) throw new ArgumentNullException(nameof(marks));
            if (cutMark == null) throw new ArgumentNullException(nameof(cutMark));
            if (saw == null) throw new ArgumentNullException(nameof(saw));

            result = null;
            BoardDimensions dimensions = piece.Dimensions;
            Length length = dimensions.Length;
            if (marks.PieceLength != length)
            {
                throw new ArgumentException("The marks belong to a piece of a different length.", nameof(marks));
            }

            if (!Contains(marks, cutMark))
            {
                return CrosscutRefusal.MarkNotOnPiece;
            }

            // Kerf on the waste side: beyond the line as seen from the reference end.
            Length line = cutMark.PositionFromStart;
            Length kerfStart = cutMark.MeasuredFrom == BoardEnd.Start ? line : line - saw.Kerf;
            Length kerfEnd = kerfStart + saw.Kerf;
            Length startLength = kerfStart;
            Length endLength = length - kerfEnd;

            if (startLength < minimumPieceLength || endLength < minimumPieceLength)
            {
                return CrosscutRefusal.PieceTooShort;
            }

            var startPiece = new TimberPiece(piece.Species,
                new BoardDimensions(startLength, dimensions.Width, dimensions.Thickness));
            var endPiece = new TimberPiece(piece.Species,
                new BoardDimensions(endLength, dimensions.Width, dimensions.Thickness));

            // Marks inside the kerf (including the cut line itself) are sawn away; the rest go to their piece.
            var startMarks = new BoardMarks(startLength);
            var endMarks = new BoardMarks(endLength);
            foreach (PencilMark mark in marks.All)
            {
                Length position = mark.PositionFromStart;
                if (position < kerfStart)
                {
                    startMarks.Add(mark);
                }
                else if (position > kerfEnd)
                {
                    endMarks.Add(new PencilMark(position - kerfEnd, mark.MeasuredFrom));
                }
            }

            double wasteVolume = saw.Kerf.Meters * dimensions.Width.Meters * dimensions.Thickness.Meters;
            var waste = new CutWaste(saw.Kerf, wasteVolume, wasteVolume * piece.Species.DensityKgPerCubicMeter);

            result = new CrosscutResult(startPiece, endPiece, startMarks, endMarks, kerfStart, kerfEnd,
                cutMark.MeasuredFrom, waste);
            return CrosscutRefusal.None;
        }

        private static bool Contains(BoardMarks marks, PencilMark mark)
        {
            foreach (PencilMark candidate in marks.All)
            {
                if (ReferenceEquals(candidate, mark))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
