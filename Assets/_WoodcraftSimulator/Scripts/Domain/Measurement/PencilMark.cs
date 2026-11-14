using System;

namespace WoodcraftSimulator.Measurement
{
    /// <summary>
    /// A pencil line across a piece. The position is stored exactly (0.1 mm) as the distance from the
    /// <see cref="BoardEnd.Start"/> end, independent of which end it was measured from; <see cref="MeasuredFrom"/>
    /// records the reference end so the intended dimension can be recovered (e.g. for a cut in Phase 4).
    /// </summary>
    public sealed class PencilMark
    {
        public PencilMark(Length positionFromStart, BoardEnd measuredFrom)
        {
            PositionFromStart = positionFromStart;
            MeasuredFrom = measuredFrom;
        }

        public Length PositionFromStart { get; }

        public BoardEnd MeasuredFrom { get; }

        /// <summary>Exact distance from the reference end to the mark.</summary>
        public Length DistanceFromReference(Length pieceLength)
        {
            if (pieceLength <= Length.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceLength), pieceLength, "Piece length must be positive.");
            }

            return MeasuredFrom == BoardEnd.Start ? PositionFromStart : pieceLength - PositionFromStart;
        }

        public override string ToString() => $"Mark at {PositionFromStart} from start (measured from {MeasuredFrom})";
    }
}
