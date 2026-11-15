using System;
using System.Collections.Generic;
using System.Text;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Economy
{
    /// <summary>Loose timber of one species and cross-section: whole stock boards and cut pieces (offcuts, parts).</summary>
    public sealed class InventoryGroup
    {
        internal InventoryGroup(string speciesId, string speciesName, Length width, Length thickness)
        {
            SpeciesId = speciesId;
            SpeciesName = speciesName;
            Width = width;
            Thickness = thickness;
        }

        public string SpeciesId { get; }

        public string SpeciesName { get; }

        public Length Width { get; }

        public Length Thickness { get; }

        /// <summary>Lengths of uncut boards, i.e. pieces exactly the size of a board in the catalog (longest first).</summary>
        public List<Length> StockBoards { get; } = new List<Length>();

        /// <summary>Lengths of cut pieces (longest first).</summary>
        public List<Length> CutPieces { get; } = new List<Length>();

        public double CubicMeters { get; internal set; }

        /// <summary>Value at the catalog price of this section; null if the merchant doesn't sell it.</summary>
        public Money? Value { get; internal set; }
    }

    /// <summary>
    /// What timber the workshop holds (master prompt §6.9, Phase 7 "track available material", "track cutting
    /// remnants"). Timber stays physical (no pocket inventory for boards): this is a summary of the loose pieces in
    /// the world, grouped by species and cross-section.
    /// </summary>
    public static class WorkshopInventory
    {
        /// <param name="pieces">Loose pieces (not installed in an assembly).</param>
        public static IReadOnlyList<InventoryGroup> Summarize(IEnumerable<TimberPiece> pieces, MaterialCatalog catalog)
        {
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var groups = new List<InventoryGroup>();
            foreach (TimberPiece piece in pieces)
            {
                BoardDimensions size = piece.Dimensions;
                InventoryGroup group = groups.Find(g => g.SpeciesId == piece.Species.Id && g.Width == size.Width &&
                                                         g.Thickness == size.Thickness);
                if (group == null)
                {
                    group = new InventoryGroup(piece.Species.Id, piece.Species.DisplayName, size.Width, size.Thickness);
                    groups.Add(group);
                }

                if (catalog.FindBoard(piece.Species.Id, size) != null)
                {
                    group.StockBoards.Add(size.Length);
                }
                else
                {
                    group.CutPieces.Add(size.Length);
                }

                group.CubicMeters += size.VolumeCubicMeters;
            }

            foreach (InventoryGroup group in groups)
            {
                group.StockBoards.Sort((a, b) => b.CompareTo(a));
                group.CutPieces.Sort((a, b) => b.CompareTo(a));
                CatalogItem price = catalog.FindBoardOfSection(group.SpeciesId,
                    new BoardDimensions(Length.FromMillimeters(1), group.Width, group.Thickness));
                group.Value = price?.ValueOfVolume(group.CubicMeters);
            }

            groups.Sort((a, b) =>
            {
                int species = string.CompareOrdinal(a.SpeciesName, b.SpeciesName);
                if (species != 0) return species;
                int width = b.Width.CompareTo(a.Width);
                return width != 0 ? width : b.Thickness.CompareTo(a.Thickness);
            });

            return groups.AsReadOnly();
        }

        /// <summary>Full stock boards of exactly this species and size among the pieces.</summary>
        public static int CountStockBoards(IEnumerable<TimberPiece> pieces, StockSpec stock)
        {
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            if (stock == null) throw new ArgumentNullException(nameof(stock));

            int count = 0;
            foreach (TimberPiece piece in pieces)
            {
                if (stock.Matches(piece.Species.Id, piece.Dimensions))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The inventory screen text: money, timber by group, fasteners.</summary>
        /// <param name="fastenerName">Fastener id → display name (unknown ids are shown as the id).</param>
        public static string Format(Ledger ledger, IReadOnlyList<InventoryGroup> groups, FastenerStock fasteners,
            Func<string, string> fastenerName = null)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (groups == null) throw new ArgumentNullException(nameof(groups));

            var text = new StringBuilder();
            text.Append(Loc.T("WORKSHOP INVENTORY\n", "ATÖLYE ENVANTERİ\n"));
            text.Append(Loc.T("Money: ", "Para: ")).Append(ledger.Balance).Append(Loc.T("   (job income ", "   (iş geliri ")).Append(ledger.TotalJobIncome)
                .Append(Loc.T(", purchases ", ", alımlar ")).Append(ledger.TotalPurchases).Append(")\n");

            text.Append(Loc.T("Timber:\n", "Ahşap:\n"));
            if (groups.Count == 0)
            {
                text.Append(Loc.T("  none\n", "  yok\n"));
            }

            foreach (InventoryGroup group in groups)
            {
                text.Append("  ").Append(group.SpeciesName).Append(' ').Append(group.Width.FormatMillimeters())
                    .Append(" × ").Append(group.Thickness).Append(':');
                if (group.Value.HasValue)
                {
                    text.Append(Loc.T("   value ", "   değer ")).Append(group.Value.Value);
                }

                text.Append('\n');
                if (group.StockBoards.Count > 0)
                {
                    text.Append(Loc.T("    boards: ", "    tahtalar: ")).Append(Lengths(group.StockBoards)).Append('\n');
                }

                if (group.CutPieces.Count > 0)
                {
                    text.Append(Loc.T("    cut pieces: ", "    kesik parçalar: ")).Append(Lengths(group.CutPieces)).Append('\n');
                }
            }

            text.Append(Loc.T("Fasteners:\n", "Vidalar:\n"));
            if (fasteners == null || fasteners.All.Count == 0)
            {
                text.Append(Loc.T("  none\n", "  yok\n"));
            }
            else
            {
                var ids = new List<string>(fasteners.All.Keys);
                ids.Sort(string.CompareOrdinal);
                foreach (string id in ids)
                {
                    text.Append("  ").Append(fasteners.Count(id)).Append(" x ").Append(fastenerName?.Invoke(id) ?? id)
                        .Append('\n');
                }
            }

            return text.ToString().TrimEnd('\n');
        }

        /// <summary>"2 x 2000, 600, 412 mm": equal lengths are counted.</summary>
        private static string Lengths(IReadOnlyList<Length> lengths)
        {
            var parts = new List<string>();
            for (int i = 0; i < lengths.Count;)
            {
                int same = 1;
                while (i + same < lengths.Count && lengths[i + same] == lengths[i])
                {
                    same++;
                }

                parts.Add(same > 1 ? $"{same} x {lengths[i].FormatMillimeters()}" : lengths[i].FormatMillimeters());
                i += same;
            }

            return string.Join(", ", parts) + " mm";
        }
    }
}
