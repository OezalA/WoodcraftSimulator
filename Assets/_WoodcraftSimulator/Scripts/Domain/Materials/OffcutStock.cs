using System;
using System.Collections.Generic;

namespace WoodcraftSimulator.Materials
{
    /// <summary>A piece kept in an offcut bin, with its surface (sanded / oiled pieces stay so).</summary>
    public sealed class StoredPiece
    {
        public StoredPiece(TimberPiece piece, BoardSurface surface)
        {
            Piece = piece ?? throw new ArgumentNullException(nameof(piece));
            Surface = surface;
        }

        public TimberPiece Piece { get; }

        public BoardSurface Surface { get; }
    }

    /// <summary>Pieces of one size and surface in a bin, e.g. "3 × 450 × 145 × 18 mm".</summary>
    public sealed class OffcutGroup
    {
        internal OffcutGroup(BoardDimensions dimensions, BoardSurface surface)
        {
            Dimensions = dimensions;
            Surface = surface;
        }

        public BoardDimensions Dimensions { get; }

        public BoardSurface Surface { get; }

        public int Count { get; internal set; }
    }

    /// <summary>
    /// The contents of one offcut bin (D-052): the apprentice sorts loose offcuts by species, one bin per species.
    /// Pieces are kept as data, not as objects; taking one gives the piece back to be made into a board again.
    /// The groups are listed longest first (then widest, thickest; raw before sanded before oiled).
    /// </summary>
    public sealed class OffcutStock
    {
        private readonly List<StoredPiece> pieces = new List<StoredPiece>();

        public OffcutStock(string speciesId)
        {
            if (string.IsNullOrWhiteSpace(speciesId))
            {
                throw new ArgumentException("A bin is for one species.", nameof(speciesId));
            }

            SpeciesId = speciesId;
        }

        public string SpeciesId { get; }

        public IReadOnlyList<StoredPiece> Pieces => pieces;

        public int Count => pieces.Count;

        public event Action Changed;

        /// <summary>Only timber of this bin's species.</summary>
        public bool Accepts(TimberPiece piece) => piece != null && piece.Species.IsTimber && piece.Species.Id == SpeciesId;

        public bool TryAdd(TimberPiece piece, BoardSurface surface = BoardSurface.Raw)
        {
            if (!Accepts(piece))
            {
                return false;
            }

            pieces.Add(new StoredPiece(piece, surface));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Replaces the contents (loading a save); pieces of another species are skipped.</summary>
        public void Restore(IEnumerable<StoredPiece> stored)
        {
            pieces.Clear();
            foreach (StoredPiece piece in stored ?? Array.Empty<StoredPiece>())
            {
                if (piece != null && Accepts(piece.Piece))
                {
                    pieces.Add(piece);
                }
            }

            Changed?.Invoke();
        }

        public IReadOnlyList<OffcutGroup> Groups()
        {
            var groups = new List<OffcutGroup>();
            foreach (StoredPiece stored in pieces)
            {
                OffcutGroup group = groups.Find(g => g.Dimensions == stored.Piece.Dimensions && g.Surface == stored.Surface);
                if (group == null)
                {
                    group = new OffcutGroup(stored.Piece.Dimensions, stored.Surface);
                    groups.Add(group);
                }

                group.Count++;
            }

            groups.Sort(Compare);
            return groups.AsReadOnly();
        }

        /// <summary>Takes one piece of the group at <paramref name="groupIndex"/> (as listed by <see cref="Groups"/>).</summary>
        public bool TryTake(int groupIndex, out StoredPiece taken)
        {
            taken = null;
            IReadOnlyList<OffcutGroup> groups = Groups();
            if (groupIndex < 0 || groupIndex >= groups.Count)
            {
                return false;
            }

            OffcutGroup group = groups[groupIndex];
            int index = pieces.FindIndex(p => p.Piece.Dimensions == group.Dimensions && p.Surface == group.Surface);
            taken = pieces[index];
            pieces.RemoveAt(index);
            Changed?.Invoke();
            return true;
        }

        private static int Compare(OffcutGroup a, OffcutGroup b)
        {
            int result = b.Dimensions.Length.CompareTo(a.Dimensions.Length);
            if (result == 0)
            {
                result = b.Dimensions.Width.CompareTo(a.Dimensions.Width);
            }

            if (result == 0)
            {
                result = b.Dimensions.Thickness.CompareTo(a.Dimensions.Thickness);
            }

            return result != 0 ? result : a.Surface.CompareTo(b.Surface);
        }
    }
}
