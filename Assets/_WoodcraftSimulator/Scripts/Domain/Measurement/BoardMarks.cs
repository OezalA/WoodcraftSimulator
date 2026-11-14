using System;
using System.Collections.Generic;

namespace WoodcraftSimulator.Measurement
{
    /// <summary>
    /// The pencil marks on one piece. Kept beside the immutable <c>TimberPiece</c> (marks are annotations that change
    /// while dimensions don't); a cut (Phase 4) will hand the marks over to the resulting pieces.
    /// </summary>
    public sealed class BoardMarks
    {
        private readonly List<PencilMark> marks = new List<PencilMark>();

        public BoardMarks(Length pieceLength)
        {
            if (pieceLength <= Length.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceLength), pieceLength, "Piece length must be positive.");
            }

            PieceLength = pieceLength;
        }

        public Length PieceLength { get; }

        public IReadOnlyList<PencilMark> All => marks;

        public int Count => marks.Count;

        /// <summary>Raised after a mark was added.</summary>
        public event Action<PencilMark> MarkAdded;

        /// <summary>Adds a mark. It must lie on the piece (0 … length, ends included).</summary>
        public void Add(PencilMark mark)
        {
            if (mark == null)
            {
                throw new ArgumentNullException(nameof(mark));
            }

            if (mark.PositionFromStart < Length.Zero || mark.PositionFromStart > PieceLength)
            {
                throw new ArgumentOutOfRangeException(nameof(mark), mark.PositionFromStart,
                    $"Mark must lie on the piece (0 … {PieceLength}).");
            }

            marks.Add(mark);
            MarkAdded?.Invoke(mark);
        }
    }
}
