using System;
using System.Collections.Generic;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Jobs
{
    /// <summary>One line of the cut list: parts of the same size.</summary>
    public sealed class CutListLine
    {
        internal CutListLine(BoardDimensions dimensions, IReadOnlyList<PartSlot> slots)
        {
            Dimensions = dimensions;
            Slots = slots;
        }

        public BoardDimensions Dimensions { get; }

        /// <summary>The blueprint slots this size is for (their names describe the line, e.g. "Left side, Right side").</summary>
        public IReadOnlyList<PartSlot> Slots { get; }

        public int Quantity => Slots.Count;

        public Length Length => Dimensions.Length;
    }

    /// <summary>
    /// How one stock board is to be cut: the parts in order and what remains. A board for narrower parts is first
    /// ripped into <see cref="Strips"/> strips of <see cref="RipWidth"/> (D-045); the parts are then cut from the strips.
    /// </summary>
    public sealed class StockBoardPlan
    {
        internal StockBoardPlan(IReadOnlyList<Length> parts, Length offcut, int cuts, Length? ripWidth = null, int strips = 0)
        {
            Parts = parts;
            Offcut = offcut;
            Cuts = cuts;
            RipWidth = ripWidth;
            Strips = strips;
        }

        public IReadOnlyList<Length> Parts { get; }

        /// <summary>Length left over after all parts and kerfs (summed over the strips of a ripped board).</summary>
        public Length Offcut { get; }

        /// <summary>Saw cuts on this board, rips included (each costs one kerf).</summary>
        public int Cuts { get; }

        /// <summary>Width the board is ripped to; null for a board cut across only.</summary>
        public Length? RipWidth { get; }

        /// <summary>Strips ripped from this board (0 if not ripped).</summary>
        public int Strips { get; }
    }

    /// <summary>
    /// Bill of materials of a blueprint made from one stock size (master prompt §6.11): the cut list (parts grouped by
    /// size), how many stock boards are needed (first-fit decreasing, every cut costs one kerf), the expected offcuts and
    /// the fasteners. A part as wide as the stock is cut across only; a narrower part (same thickness) is cut from a
    /// strip ripped along the board (D-045): boards for one width give as many strips as fit, and the strips are packed
    /// like boards. Different widths are not mixed on one board (simple, slightly generous). The plan is a
    /// recommendation; the player may cut differently.
    /// </summary>
    public sealed class MaterialPlan
    {
        private MaterialPlan(StockSpec stock, Length kerf, IReadOnlyList<CutListLine> lines, IReadOnlyList<StockBoardPlan> boards,
            IReadOnlyDictionary<string, int> fasteners, IReadOnlyList<string> problems)
        {
            Stock = stock;
            Kerf = kerf;
            Lines = lines;
            Boards = boards;
            Fasteners = fasteners;
            Problems = problems;
        }

        public StockSpec Stock { get; }

        public Length Kerf { get; }

        public IReadOnlyList<CutListLine> Lines { get; }

        /// <summary>One entry per stock board needed.</summary>
        public IReadOnlyList<StockBoardPlan> Boards { get; }

        public int BoardsNeeded => Boards.Count;

        /// <summary>Fastener id → number needed.</summary>
        public IReadOnlyDictionary<string, int> Fasteners { get; }

        /// <summary>Why the blueprint can't be made from this stock (empty = it can).</summary>
        public IReadOnlyList<string> Problems { get; }

        public bool IsFeasible => Problems.Count == 0;

        /// <summary>Whether any part has to be ripped from the stock.</summary>
        public bool NeedsRipping
        {
            get
            {
                foreach (StockBoardPlan board in Boards)
                {
                    if (board.RipWidth.HasValue)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static MaterialPlan Calculate(Blueprint blueprint, StockSpec stock, Length kerf)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stock == null) throw new ArgumentNullException(nameof(stock));
            if (kerf < Length.Zero) throw new ArgumentOutOfRangeException(nameof(kerf), "Kerf must not be negative.");

            // Cut list: parts of the same size together, in blueprint order.
            var lines = new List<CutListLine>();
            var slotsBySize = new Dictionary<BoardDimensions, List<PartSlot>>();
            var problems = new List<string>();
            var lengthsByWidth = new SortedDictionary<Length, List<Length>>();
            BoardDimensions stockSize = stock.Dimensions;
            foreach (PartSlot slot in blueprint.Slots)
            {
                if (!slotsBySize.TryGetValue(slot.Dimensions, out List<PartSlot> group))
                {
                    group = new List<PartSlot>();
                    slotsBySize.Add(slot.Dimensions, group);
                    lines.Add(new CutListLine(slot.Dimensions, group));
                }

                group.Add(slot);

                Length width = slot.Dimensions.Width;
                bool widthWorks = width == stockSize.Width || width + kerf + Ripcut.MinimumWidth <= stockSize.Width;
                if (slot.Dimensions.Thickness != stockSize.Thickness || !widthWorks)
                {
                    problems.Add(Loc.T(
                        $"{slot.DisplayName}: needs {slot.Dimensions.Width} × {slot.Dimensions.Thickness}, the stock is {stockSize.Width} × {stockSize.Thickness}.",
                        $"{slot.DisplayName}: {slot.Dimensions.Width} × {slot.Dimensions.Thickness} gerekli, malzeme {stockSize.Width} × {stockSize.Thickness}."));
                }
                else if (slot.Dimensions.Length > stockSize.Length)
                {
                    problems.Add(Loc.T(
                        $"{slot.DisplayName}: {slot.Dimensions.Length} is longer than the stock ({stockSize.Length}).",
                        $"{slot.DisplayName}: {slot.Dimensions.Length} malzemeden ({stockSize.Length}) uzun."));
                }
                else
                {
                    if (!lengthsByWidth.TryGetValue(width, out List<Length> lengths))
                    {
                        lengths = new List<Length>();
                        lengthsByWidth.Add(width, lengths);
                    }

                    lengths.Add(slot.Dimensions.Length);
                }
            }

            var boards = new List<StockBoardPlan>();
            if (lengthsByWidth.TryGetValue(stockSize.Width, out List<Length> fullWidth))
            {
                foreach (Bin bin in Pack(fullWidth, stockSize.Length, kerf))
                {
                    boards.Add(new StockBoardPlan(bin.Parts.AsReadOnly(), bin.Remaining, bin.Cuts));
                }
            }

            // Narrower parts: pack them into full-length strips, then rip as many strips per board as fit.
            foreach (KeyValuePair<Length, List<Length>> group in lengthsByWidth)
            {
                Length width = group.Key;
                if (width == stockSize.Width)
                {
                    continue;
                }

                List<Bin> strips = Pack(group.Value, stockSize.Length, kerf);
                int perBoard = (int)((stockSize.Width + kerf).TenthsOfMillimeter / (width + kerf).TenthsOfMillimeter);
                for (int first = 0; first < strips.Count; first += perBoard)
                {
                    int count = Math.Min(perBoard, strips.Count - first);
                    var parts = new List<Length>();
                    Length offcut = Length.Zero;
                    int cuts = count; // one rip per strip (the last strip leaves a narrow rest)
                    for (int i = first; i < first + count; i++)
                    {
                        parts.AddRange(strips[i].Parts);
                        offcut += strips[i].Remaining;
                        cuts += strips[i].Cuts;
                    }

                    boards.Add(new StockBoardPlan(parts.AsReadOnly(), offcut, cuts, width, count));
                }
            }

            var fasteners = new Dictionary<string, int>();
            foreach (ConnectionSpec connection in blueprint.Connections)
            {
                fasteners.TryGetValue(connection.FastenerId, out int count);
                fasteners[connection.FastenerId] = count + connection.FastenerCount;
            }

            return new MaterialPlan(stock, kerf, lines.AsReadOnly(), boards.AsReadOnly(), fasteners, problems.AsReadOnly());
        }

        private sealed class Bin
        {
            public readonly List<Length> Parts = new List<Length>();
            public Length Remaining;
            public int Cuts;
        }

        /// <summary>First-fit decreasing: longest parts first, each into the first bin with room left.</summary>
        private static List<Bin> Pack(List<Length> lengths, Length binLength, Length kerf)
        {
            var sorted = new List<Length>(lengths);
            sorted.Sort((a, b) => b.CompareTo(a));
            var bins = new List<Bin>();
            foreach (Length part in sorted)
            {
                Bin chosen = bins.Find(bin => bin.Remaining >= part);
                if (chosen == null)
                {
                    chosen = new Bin { Remaining = binLength };
                    bins.Add(chosen);
                }

                chosen.Parts.Add(part);
                Length left = chosen.Remaining - part;
                if (left > Length.Zero)
                {
                    // Cutting the part off costs a kerf (or eats the rest if less than a kerf remains).
                    left = left > kerf ? left - kerf : Length.Zero;
                    chosen.Cuts++;
                }

                chosen.Remaining = left;
            }

            return bins;
        }
    }
}
