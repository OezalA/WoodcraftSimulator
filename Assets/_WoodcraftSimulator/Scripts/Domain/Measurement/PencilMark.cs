using System;

namespace WoodcraftSimulator.Measurement
{
    /// <summary>
    /// The two broad faces of a piece. In the 3D representation, Top is the local +Y face and Bottom the −Y face.
    /// </summary>
    public enum BoardFace
    {
        Top,
        Bottom
    }

    /// <summary>
    /// A pencil line across a piece. The position is stored exactly (0.1 mm) as the distance from the
    /// <see cref="BoardEnd.Start"/> end, independent of which end it was measured from; <see cref="MeasuredFrom"/>
    /// records the reference end so the intended dimension can be recovered (e.g. for a cut).
    /// </summary>
    public sealed class PencilMark
    {
        public PencilMark(Length positionFromStart, BoardEnd measuredFrom, BoardFace face = BoardFace.Top)
        {
            PositionFromStart = positionFromStart;
            MeasuredFrom = measuredFrom;
            Face = face;
        }

        public Length PositionFromStart { get; }

        public BoardEnd MeasuredFrom { get; }

        /// <summary>The face the line is drawn on.</summary>
        public BoardFace Face { get; }

        /// <summary>Exact distance from the reference end to the mark.</summary>
        public Length DistanceFromReference(Length pieceLength)
        {
            if (pieceLength <= Length.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceLength), pieceLength, "Piece length must be positive.");
            }

            return MeasuredFrom == BoardEnd.Start ? PositionFromStart : pieceLength - PositionFromStart;
        }

        public override string ToString() =>
            $"Mark at {PositionFromStart} from start (measured from {MeasuredFrom}, {Face} face)";
    }
}
