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
        internal StockBoardPlan(StockSpec stock, IReadOnlyList<Length> parts, Length offcut, int cuts, Length? ripWidth = null,
            int strips = 0)
        {
            Stock = stock;
            Parts = parts;
            Offcut = offcut;
            Cuts = cuts;
            RipWidth = ripWidth;
            Strips = strips;
        }

        /// <summary>The stock this board is (a job may use several, D-046).</summary>
        public StockSpec Stock { get; }

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
        private MaterialPlan(IReadOnlyList<StockSpec> stocks, Length kerf, IReadOnlyList<CutListLine> lines,
            IReadOnlyList<StockBoardPlan> boards, IReadOnlyDictionary<string, int> fasteners, IReadOnlyList<string> problems)
        {
            Stocks = stocks;
            Kerf = kerf;
            Lines = lines;
            Boards = boards;
            Fasteners = fasteners;
            Problems = problems;
        }

        /// <summary>The main stock (the first one of the job).</summary>
        public StockSpec Stock => Stocks[0];

        /// <summary>Every stock the job may use; each part is planned from the one that fits it best (D-046).</summary>
        public IReadOnlyList<StockSpec> Stocks { get; }

        public Length Kerf { get; }

        /// <summary>Boards of one stock the plan needs.</summary>
        public int BoardsNeededFor(StockSpec stock)
        {
            int count = 0;
            foreach (StockBoardPlan board in Boards)
            {
                if (board.Stock == stock)
                {
                    count++;
                }
            }

            return count;
        }

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
            if (stock == null) throw new ArgumentNullException(nameof(stock));

            return Calculate(blueprint, new[] { stock }, kerf);
        }

        /// <summary>
        /// Plans every part from the stock that fits it best (D-046): same thickness, wide enough (exactly as wide, or a
        /// strip can be ripped), long enough; an exact width is preferred, then the narrowest, then the shortest stock.
        /// </summary>
        public static MaterialPlan Calculate(Blueprint blueprint, IReadOnlyList<StockSpec> stocks, Length kerf)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stocks == null || stocks.Count == 0) throw new ArgumentException("At least one stock is needed.", nameof(stocks));
            if (kerf < Length.Zero) throw new ArgumentOutOfRangeException(nameof(kerf), "Kerf must not be negative.");

            // Cut list: parts of the same size together, in blueprint order.
            var lines = new List<CutListLine>();
            var slotsBySize = new Dictionary<BoardDimensions, List<PartSlot>>();
            var problems = new List<string>();
            var groups = new List<(StockSpec stock, Length width, List<Length> lengths)>();
            foreach (PartSlot slot in blueprint.Slots)
            {
                if (!slotsBySize.TryGetValue(slot.Dimensions, out List<PartSlot> group))
                {
                    group = new List<PartSlot>();
                    slotsBySize.Add(slot.Dimensions, group);
                    lines.Add(new CutListLine(slot.Dimensions, group));
                }

                group.Add(slot);

                StockSpec chosen = BestStock(slot.Dimensions, stocks, kerf);
                if (chosen == null)
                {
                    BoardDimensions main = stocks[0].Dimensions;
                    problems.Add(slot.Dimensions.Length > main.Length && slot.Dimensions.Width == main.Width &&
                                 slot.Dimensions.Thickness == main.Thickness
                        ? Loc.T($"{slot.DisplayName}: {slot.Dimensions.Length} is longer than the stock ({main.Length}).",
                            $"{slot.DisplayName}: {slot.Dimensions.Length} malzemeden ({main.Length}) uzun.")
                        : Loc.T($"{slot.DisplayName}: needs {slot.Dimensions.Width} × {slot.Dimensions.Thickness}, the stock is {main.Width} × {main.Thickness}.",
                            $"{slot.DisplayName}: {slot.Dimensions.Width} × {slot.Dimensions.Thickness} gerekli, malzeme {main.Width} × {main.Thickness}."));
                    continue;
                }

                int index = groups.FindIndex(g => g.stock == chosen && g.width == slot.Dimensions.Width);
                if (index < 0)
                {
                    groups.Add((chosen, slot.Dimensions.Width, new List<Length>()));
                    index = groups.Count - 1;
                }

                groups[index].lengths.Add(slot.Dimensions.Length);
            }

            // Stocks in the job's order; per stock the parts cut across first, then the ripped widths (narrow to wide).
            groups.Sort((a, b) =>
            {
                int byStock = IndexOf(stocks, a.stock).CompareTo(IndexOf(stocks, b.stock));
                if (byStock != 0) return byStock;
                bool aFull = a.width == a.stock.Dimensions.Width;
                bool bFull = b.width == b.stock.Dimensions.Width;
                if (aFull != bFull) return aFull ? -1 : 1;
                return a.width.CompareTo(b.width);
            });

            var boards = new List<StockBoardPlan>();
            foreach ((StockSpec stock, Length width, List<Length> lengths) in groups)
            {
                BoardDimensions stockSize = stock.Dimensions;
                List<Bin> bins = Pack(lengths, stockSize.Length, kerf);
                if (width == stockSize.Width)
                {
                    foreach (Bin bin in bins)
                    {
                        boards.Add(new StockBoardPlan(stock, bin.Parts.AsReadOnly(), bin.Remaining, bin.Cuts));
                    }

                    continue;
                }

                // Narrower parts: full-length strips, as many ripped from one board as fit (D-045).
                int perBoard = (int)((stockSize.Width + kerf).TenthsOfMillimeter / (width + kerf).TenthsOfMillimeter);
                for (int first = 0; first < bins.Count; first += perBoard)
                {
                    int count = Math.Min(perBoard, bins.Count - first);
                    var parts = new List<Length>();
                    Length offcut = Length.Zero;
                    int cuts = count; // one rip per strip (the last strip leaves a narrow rest)
                    for (int i = first; i < first + count; i++)
                    {
                        parts.AddRange(bins[i].Parts);
                        offcut += bins[i].Remaining;
                        cuts += bins[i].Cuts;
                    }

                    boards.Add(new StockBoardPlan(stock, parts.AsReadOnly(), offcut, cuts, width, count));
                }
            }

            var fasteners = new Dictionary<string, int>();
            foreach (ConnectionSpec connection in blueprint.Connections)
            {
                fasteners.TryGetValue(connection.FastenerId, out int count);
                fasteners[connection.FastenerId] = count + connection.FastenerCount;
            }

            return new MaterialPlan(new List<StockSpec>(stocks).AsReadOnly(), kerf, lines.AsReadOnly(), boards.AsReadOnly(),
                fasteners, problems.AsReadOnly());
        }

        private static StockSpec BestStock(BoardDimensions part, IReadOnlyList<StockSpec> stocks, Length kerf)
        {
            StockSpec best = null;
            foreach (StockSpec stock in stocks)
            {
                BoardDimensions size = stock.Dimensions;
                bool widthWorks = part.Width == size.Width || part.Width + kerf + Ripcut.MinimumWidth <= size.Width;
                if (part.Thickness != size.Thickness || !widthWorks || part.Length > size.Length)
                {
                    continue;
                }

                if (best == null || Better(stock, best, part))
                {
                    best = stock;
                }
            }

            return best;
        }

        private static bool Better(StockSpec candidate, StockSpec current, BoardDimensions part)
        {
            bool candidateExact = candidate.Dimensions.Width == part.Width;
            bool currentExact = current.Dimensions.Width == part.Width;
            if (candidateExact != currentExact) return candidateExact;
            int byWidth = candidate.Dimensions.Width.CompareTo(current.Dimensions.Width);
            if (byWidth != 0) return byWidth < 0;
            return candidate.Dimensions.Length < current.Dimensions.Length;
        }

        private static int IndexOf(IReadOnlyList<StockSpec> stocks, StockSpec stock)
        {
            for (int i = 0; i < stocks.Count; i++)
            {
                if (stocks[i] == stock)
                {
                    return i;
                }
            }

            return -1;
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
