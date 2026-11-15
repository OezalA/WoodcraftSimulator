using System;
using System.Collections.Generic;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Economy;

namespace WoodcraftSimulator.Jobs
{
    /// <summary>One line of a priced bill of materials: what the job needs, what the workshop has, what to buy.</summary>
    public sealed class ShoppingLine
    {
        internal ShoppingLine(string description, CatalogItem item, int needed, int available, int unitsToBuy)
        {
            Description = description;
            Item = item;
            Needed = needed;
            Available = available;
            UnitsToBuy = unitsToBuy;
        }

        /// <summary>What is needed, e.g. "Spruce board 2000 × 145 × 18 mm" or "Wood screw 4 × 40 mm".</summary>
        public string Description { get; }

        /// <summary>The catalog item to buy it as; null if the merchant doesn't sell it.</summary>
        public CatalogItem Item { get; }

        /// <summary>Boards or fasteners the job needs.</summary>
        public int Needed { get; }

        /// <summary>Matching boards or fasteners already in the workshop.</summary>
        public int Available { get; }

        /// <summary>Catalog units to buy (boards, or boxes for fasteners); 0 if the workshop has enough.</summary>
        public int UnitsToBuy { get; }

        public Money Cost => Item == null ? Money.Zero : Item.UnitPrice * UnitsToBuy;

        public bool IsSold => Item != null;
    }

    /// <summary>
    /// The bill of materials of a job priced against the catalog and the workshop's stock (master prompt §6.11,
    /// Phase 7 "plan and purchase"): stock boards and fasteners needed, what is already there and what to buy.
    /// </summary>
    public sealed class ShoppingList
    {
        private ShoppingList(IReadOnlyList<ShoppingLine> lines)
        {
            Lines = lines;
        }

        public IReadOnlyList<ShoppingLine> Lines { get; }

        public Money TotalCost
        {
            get
            {
                Money total = Money.Zero;
                foreach (ShoppingLine line in Lines)
                {
                    total += line.Cost;
                }

                return total;
            }
        }

        /// <param name="boardsAvailable">Full stock boards of the plan's stock lying in the workshop.</param>
        /// <param name="fastenerName">Fastener id → display name (unknown ids are shown as the id).</param>
        public static ShoppingList For(MaterialPlan plan, MaterialCatalog catalog, int boardsAvailable,
            FastenerStock fasteners, Func<string, string> fastenerName = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (boardsAvailable < 0) throw new ArgumentOutOfRangeException(nameof(boardsAvailable), boardsAvailable, "Must not be negative.");

            var lines = new List<ShoppingLine>();
            CatalogItem board = catalog.FindBoard(plan.Stock);
            int boardsToBuy = Math.Max(0, plan.BoardsNeeded - boardsAvailable);
            lines.Add(new ShoppingLine(plan.Stock.ToString(), board, plan.BoardsNeeded, boardsAvailable,
                board == null ? 0 : boardsToBuy));

            foreach (KeyValuePair<string, int> needed in plan.Fasteners)
            {
                CatalogItem box = catalog.FindFastenerBox(needed.Key);
                int available = fasteners?.Count(needed.Key) ?? 0;
                int missing = Math.Max(0, needed.Value - available);
                int boxes = box == null ? 0 : (missing + box.BoxSize - 1) / box.BoxSize;
                lines.Add(new ShoppingLine(fastenerName?.Invoke(needed.Key) ?? needed.Key, box, needed.Value, available, boxes));
            }

            return new ShoppingList(lines.AsReadOnly());
        }
    }

    /// <summary>
    /// The money side of a delivered job (D-040): the pay earned and the cost of what the job used, so the profit.
    /// Pay (owner's choice K2): <see cref="BaseShare"/> of the job's pay for delivering it, the rest split evenly over
    /// the quality goals met. Material cost (K3): the timber used (in the structure plus sawdust, prorated from the
    /// board price) and the fasteners driven; offcuts stay in the workshop with their value.
    /// </summary>
    public sealed class JobSettlement
    {
        /// <summary>Share of the pay earned by delivering the job, whatever the quality goals.</summary>
        public const double BaseShare = 0.7;

        private JobSettlement(Money pay, Money basePay, Money bonus, Money timberCost, Money fastenerCost,
            IReadOnlyList<string> unpriced)
        {
            Pay = pay;
            BasePay = basePay;
            Bonus = bonus;
            TimberCost = timberCost;
            FastenerCost = fastenerCost;
            Unpriced = unpriced;
        }

        /// <summary>The job's full pay (all goals met).</summary>
        public Money Pay { get; }

        public Money BasePay { get; }

        /// <summary>Earned for the quality goals met.</summary>
        public Money Bonus { get; }

        public Money Earned => BasePay + Bonus;

        public Money TimberCost { get; }

        public Money FastenerCost { get; }

        public Money MaterialCost => TimberCost + FastenerCost;

        public Money Profit => Earned - MaterialCost;

        /// <summary>Material used that the catalog has no price for (costed at zero).</summary>
        public IReadOnlyList<string> Unpriced { get; }

        public static JobSettlement Calculate(JobResult result, MaterialCatalog catalog)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            Money pay = result.Job.Pay;
            Money basePay = pay.Scale(BaseShare);
            Money bonusPool = pay - basePay;
            Money bonus = result.Goals.Count == 0
                ? bonusPool
                : bonusPool.Scale((double)result.GoalsMet / result.Goals.Count);

            var unpriced = new List<string>();
            Job job = result.Job;
            CatalogItem board = catalog.FindBoard(job.Stock) ??
                                catalog.FindBoardOfSection(job.Stock.SpeciesId, job.Stock.Dimensions);
            double used = result.Material.InstalledCubicMeters + result.Material.SawdustCubicMeters;
            Money timber = Money.Zero;
            if (board != null)
            {
                timber = board.ValueOfVolume(used);
            }
            else if (used > 0.0)
            {
                unpriced.Add(job.Stock.ToString());
            }

            var driven = new Dictionary<string, int>();
            foreach (FastenerQuality fastener in result.Report.Fasteners)
            {
                string id = fastener.Connection.FastenerId;
                driven.TryGetValue(id, out int count);
                driven[id] = count + 1;
            }

            Money fasteners = Money.Zero;
            foreach (KeyValuePair<string, int> entry in driven)
            {
                CatalogItem box = catalog.FindFastenerBox(entry.Key);
                if (box != null)
                {
                    fasteners += box.ValueOfFasteners(entry.Value);
                }
                else
                {
                    unpriced.Add(entry.Key);
                }
            }

            return new JobSettlement(pay, basePay, bonus, timber, fasteners, unpriced.AsReadOnly());
        }
    }
}
