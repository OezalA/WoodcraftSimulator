using System;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Cutting
{
    public enum RipRefusal
    {
        None,

        /// <summary>The ripped piece or the rest would be narrower than the minimum width.</summary>
        TooNarrow
    }

    /// <summary>The two pieces of a rip cut: the strip at the set width, and the rest of the board's width.</summary>
    public sealed class RipResult
    {
        internal RipResult(TimberPiece strip, TimberPiece rest, BoardMarks stripMarks, BoardMarks restMarks, CutWaste waste)
        {
            Strip = strip;
            Rest = rest;
            StripMarks = stripMarks;
            RestMarks = restMarks;
            Waste = waste;
        }

        /// <summary>The piece with exactly the set width (measured from the board's edge, the fence side).</summary>
        public TimberPiece Strip { get; }

        /// <summary>What remains: the board's width minus the strip and the kerf.</summary>
        public TimberPiece Rest { get; }

        public BoardMarks StripMarks { get; }

        public BoardMarks RestMarks { get; }

        public CutWaste Waste { get; }
    }

    /// <summary>
    /// A cut along the length (Phase 9a, D-045): the board is split into a strip of the set width and the rest; the
    /// kerf goes into the rest (the strip keeps the set width, as a carpenter cuts on the waste side). Pencil marks are
    /// positions along the length, so both pieces keep them.
    /// </summary>
    public static class Ripcut
    {
        public static Length MinimumWidth => Length.FromMillimeters(10);

        public static RipRefusal TryRip(TimberPiece piece, BoardMarks marks, Length width, Saw saw, out RipResult result)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (marks == null) throw new ArgumentNullException(nameof(marks));
            if (saw == null) throw new ArgumentNullException(nameof(saw));

            result = null;
            BoardDimensions size = piece.Dimensions;
            Length rest = size.Width - width - saw.Kerf;
            if (width < MinimumWidth || rest < MinimumWidth)
            {
                return RipRefusal.TooNarrow;
            }

            var strip = new TimberPiece(piece.Species, new BoardDimensions(size.Length, width, size.Thickness));
            var restPiece = new TimberPiece(piece.Species, new BoardDimensions(size.Length, rest, size.Thickness));
            double volume = saw.Kerf.Meters * size.Length.Meters * size.Thickness.Meters;
            var waste = new CutWaste(saw.Kerf, volume, volume * piece.Species.DensityKgPerCubicMeter);
            result = new RipResult(strip, restPiece, Copy(marks), Copy(marks), waste);
            return RipRefusal.None;
        }

        private static BoardMarks Copy(BoardMarks marks)
        {
            var copy = new BoardMarks(marks.PieceLength);
            foreach (PencilMark mark in marks.All)
            {
                copy.Add(new PencilMark(mark.PositionFromStart, mark.MeasuredFrom, mark.Face));
            }

            return copy;
        }
    }
}
