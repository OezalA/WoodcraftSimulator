using System;

namespace WoodcraftSimulator.Measurement
{
    /// <summary>
    /// The two ends of a piece along its length axis. In the 3D representation, Start is the local −Z end and
    /// End the local +Z end (see BoardDimensions).
    /// </summary>
    public enum BoardEnd
    {
        Start,
        End
    }

    /// <summary>
    /// Tape measure rules: the hook sits on one end of the piece; the reading is the distance from that end,
    /// limited to the piece, rounded to the tape's graduation.
    /// Positions along the piece are given as the distance from the <see cref="BoardEnd.Start"/> end.
    /// </summary>
    public static class TapeMeasure
    {
        /// <summary>Typical tape graduation: 1 mm.</summary>
        public static Length DefaultResolution => Length.FromMillimeters(1);

        /// <summary>Distance from the hooked end to a point, clamped to the piece, before rounding.</summary>
        public static Length DistanceFromHook(Length pieceLength, Length pointFromStart, BoardEnd hookedEnd)
        {
            RequirePositive(pieceLength, nameof(pieceLength));
            Length onPiece = Length.Clamp(pointFromStart, Length.Zero, pieceLength);
            return hookedEnd == BoardEnd.Start ? onPiece : pieceLength - onPiece;
        }

        /// <summary>What the tape shows at the point: <see cref="DistanceFromHook"/> rounded to the graduation.</summary>
        public static Length Read(Length pieceLength, Length pointFromStart, BoardEnd hookedEnd, Length resolution)
        {
            return DistanceFromHook(pieceLength, pointFromStart, hookedEnd).RoundTo(resolution);
        }

        /// <summary>
        /// The end closest to a position along the piece axis (e.g. the player's position projected onto it).
        /// The position may lie outside the piece. Ties go to <see cref="BoardEnd.Start"/>.
        /// </summary>
        public static BoardEnd NearestEnd(Length pieceLength, Length positionFromStart)
        {
            RequirePositive(pieceLength, nameof(pieceLength));
            Length toStart = positionFromStart.Abs();
            Length toEnd = (pieceLength - positionFromStart).Abs();
            return toStart <= toEnd ? BoardEnd.Start : BoardEnd.End;
        }

        private static void RequirePositive(Length value, string parameterName)
        {
            if (value <= Length.Zero)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Piece length must be positive.");
            }
        }
    }
}
