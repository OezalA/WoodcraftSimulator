using System;
using System.Collections.Generic;
using WoodcraftSimulator.Construction;
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

    /// <summary>How one stock board is to be cut: the parts in order, and what remains.</summary>
    public sealed class StockBoardPlan
    {
        internal StockBoardPlan(IReadOnlyList<Length> parts, Length offcut, int cuts)
        {
            Parts = parts;
            Offcut = offcut;
            Cuts = cuts;
        }

        public IReadOnlyList<Length> Parts { get; }

        /// <summary>Length left over after all parts and kerfs (zero if the last part used the board's end).</summary>
        public Length Offcut { get; }

        /// <summary>Saw cuts on this board (each costs one kerf).</summary>
        public int Cuts { get; }
    }

    /// <summary>
    /// Bill of materials of a blueprint made from one stock size (master prompt §6.11): the cut list (parts grouped by
    /// size), how many stock boards are needed (first-fit decreasing, every cut costs one kerf), the expected offcuts and
    /// the fasteners. Only crosscuts: a part must have the stock's width and thickness (no ripping yet).
    /// The plan is a recommendation; the player may cut differently.
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

        public static MaterialPlan Calculate(Blueprint blueprint, StockSpec stock, Length kerf)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stock == null) throw new ArgumentNullException(nameof(stock));
            if (kerf < Length.Zero) throw new ArgumentOutOfRangeException(nameof(kerf), "Kerf must not be negative.");

            // Cut list: parts of the same size together, in blueprint order.
            var lines = new List<CutListLine>();
            var slotsBySize = new Dictionary<BoardDimensions, List<PartSlot>>();
            var problems = new List<string>();
            var lengths = new List<Length>();
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

                if (slot.Dimensions.Width != stockSize.Width || slot.Dimensions.Thickness != stockSize.Thickness)
                {
                    problems.Add(Localization.Loc.T(
                        $"{slot.DisplayName}: needs {slot.Dimensions.Width} × {slot.Dimensions.Thickness}, the stock is {stockSize.Width} × {stockSize.Thickness} (no rip cuts yet).",
                        $"{slot.DisplayName}: {slot.Dimensions.Width} × {slot.Dimensions.Thickness} gerekli, malzeme {stockSize.Width} × {stockSize.Thickness} (boyuna kesim henüz yok)."));
                }
                else if (slot.Dimensions.Length > stockSize.Length)
                {
                    problems.Add(Localization.Loc.T(
                        $"{slot.DisplayName}: {slot.Dimensions.Length} is longer than the stock ({stockSize.Length}).",
                        $"{slot.DisplayName}: {slot.Dimensions.Length} malzemeden ({stockSize.Length}) uzun."));
                }
                else
                {
                    lengths.Add(slot.Dimensions.Length);
                }
            }

            // First-fit decreasing: longest parts first, each into the first board with room left.
            lengths.Sort((a, b) => b.CompareTo(a));
            var boardParts = new List<List<Length>>();
            var remaining = new List<Length>();
            var cuts = new List<int>();
            foreach (Length part in lengths)
            {
                int chosen = -1;
                for (int i = 0; i < remaining.Count && chosen < 0; i++)
                {
                    if (remaining[i] >= part)
                    {
                        chosen = i;
                    }
                }

                if (chosen < 0)
                {
                    boardParts.Add(new List<Length>());
                    remaining.Add(stockSize.Length);
                    cuts.Add(0);
                    chosen = boardParts.Count - 1;
                }

                boardParts[chosen].Add(part);
                Length left = remaining[chosen] - part;
                if (left > Length.Zero)
                {
                    // Cutting the part off costs a kerf (or eats the rest if less than a kerf remains).
                    left = left > kerf ? left - kerf : Length.Zero;
                    cuts[chosen]++;
                }

                remaining[chosen] = left;
            }

            var boards = new List<StockBoardPlan>();
            for (int i = 0; i < boardParts.Count; i++)
            {
                boards.Add(new StockBoardPlan(boardParts[i].AsReadOnly(), remaining[i], cuts[i]));
            }

            var fasteners = new Dictionary<string, int>();
            foreach (ConnectionSpec connection in blueprint.Connections)
            {
                fasteners.TryGetValue(connection.FastenerId, out int count);
                fasteners[connection.FastenerId] = count + connection.FastenerCount;
            }

            return new MaterialPlan(stock, kerf, lines.AsReadOnly(), boards.AsReadOnly(), fasteners, problems.AsReadOnly());
        }
    }
}
