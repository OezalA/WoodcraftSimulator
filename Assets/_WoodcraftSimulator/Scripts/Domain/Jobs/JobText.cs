using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Jobs
{
    /// <summary>
    /// Plain-text forms of jobs for the HUD: the job sheet (description, bill of materials, cut list with progress)
    /// and the results. Avoid symbols outside Latin-1 / Turkish letters (D-028, D-041).
    /// </summary>
    public static class JobText
    {
        /// <param name="fastenerName">Fastener id → display name (unknown ids are shown as the id).</param>
        public static string Sheet(Job job, MaterialPlan plan, IReadOnlyList<CutListLineProgress> progress,
            Func<string, string> fastenerName, int boardsDelivered, double seconds)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var text = new StringBuilder();
            text.Append(Loc.T("JOB: ", "İŞ: ")).Append(job.DisplayName).Append("   (").Append(FormatTime(seconds)).Append(")\n");
            if (job.Description.Length > 0)
            {
                text.Append(job.Description).Append('\n');
            }

            text.Append(Loc.T($"Stock: {boardsDelivered} x {plan.Stock} for this job\n",
                $"Malzeme: bu iş için {boardsDelivered} x {plan.Stock}\n"));
            text.Append(Loc.T("Cut list:\n", "Kesim listesi:\n"));
            for (int i = 0; i < plan.Lines.Count; i++)
            {
                CutListLine line = plan.Lines[i];
                text.Append("  ").Append(line.Quantity).Append(" x ").Append(line.Length).Append("  ")
                    .Append(SlotNames(line));
                if (progress != null && i < progress.Count)
                {
                    CutListLineProgress state = progress[i];
                    text.Append(state.IsDone
                        ? Loc.T("  - done", "  - bitti")
                        : Loc.T($"  - {state.Installed} in, {state.Ready} ready", $"  - {state.Installed} takılı, {state.Ready} hazır"));
                }

                text.Append('\n');
            }

            AppendFasteners(text, plan, fastenerName);
            foreach (string tip in job.Tips)
            {
                text.Append(Loc.T("Tip: ", "İpucu: ")).Append(tip).Append('\n');
            }

            return text.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// The bill of materials before accepting: pay, stock needed, cut plan per board, expected offcuts, and what
        /// to buy when a <paramref name="shopping"/> list is given.
        /// </summary>
        public static string Offer(Job job, MaterialPlan plan, Func<string, string> fastenerName,
            ShoppingList shopping = null)
        {
            var text = new StringBuilder();
            text.Append(job.DisplayName);
            if (job.Pay > Money.Zero)
            {
                text.Append(Loc.T("   Pay ", "   Ücret ")).Append(job.Pay);
            }

            text.Append('\n');
            if (job.Description.Length > 0)
            {
                text.Append(job.Description).Append('\n');
            }

            if (!plan.IsFeasible)
            {
                text.Append(Loc.T("Can't be made from the stock: ", "Bu malzemeden yapılamaz: ")).Append(string.Join(" ", plan.Problems));
                return text.ToString();
            }

            text.Append(Loc.T("Material: ", "Malzeme: ")).Append(plan.BoardsNeeded).Append(" x ").Append(plan.Stock);
            if (job.SpareBoards > 0)
            {
                text.Append(" + ").Append(job.SpareBoards).Append(Loc.T(" spare", " yedek"));
            }

            text.Append('\n');
            for (int i = 0; i < plan.Boards.Count; i++)
            {
                StockBoardPlan board = plan.Boards[i];
                var parts = new List<string>();
                foreach (Length part in board.Parts)
                {
                    parts.Add(part.FormatMillimeters());
                }

                text.Append(Loc.T("  Board ", "  Tahta ")).Append(i + 1).Append(": ").Append(string.Join(" + ", parts))
                    .Append(Loc.T(" mm, offcut ", " mm, artık ")).Append(board.Offcut).Append('\n');
            }

            AppendFasteners(text, plan, fastenerName);
            text.Append(Loc.T($"(kerf {plan.Kerf} per cut)", $"(her kesimde {plan.Kerf} testere payı)"));
            if (shopping != null)
            {
                text.Append('\n').Append(Shopping(shopping));
            }

            return text.ToString();
        }

        /// <summary>What the workshop has and what must be bought, e.g. "2 x Spruce board ...: have 0, buy 2 = €17.00".</summary>
        public static string Shopping(ShoppingList shopping)
        {
            if (shopping == null) throw new ArgumentNullException(nameof(shopping));

            var text = new StringBuilder();
            text.Append(Loc.T("In the workshop / to buy:\n", "Atölyede / alınacak:\n"));
            foreach (ShoppingLine line in shopping.Lines)
            {
                text.Append("  ").Append(line.Needed).Append(" x ").Append(line.Description)
                    .Append(Loc.T(": have ", ": var ")).Append(line.Available);
                if (!line.IsSold)
                {
                    text.Append(line.Available >= line.Needed
                        ? Loc.T(", enough", ", yeterli")
                        : Loc.T(", not sold by the merchant", ", satıcıda yok"));
                }
                else if (line.UnitsToBuy == 0)
                {
                    text.Append(Loc.T(", enough", ", yeterli"));
                }
                else
                {
                    text.Append(Loc.T(", buy ", ", al: ")).Append(line.UnitsToBuy).Append(" x ").Append(line.Item.DisplayName)
                        .Append(" = ").Append(line.Cost);
                }

                text.Append('\n');
            }

            text.Append(Loc.T("Total to buy: ", "Alınacakların toplamı: ")).Append(shopping.TotalCost);
            return text.ToString();
        }

        public static string Result(JobResult result, string fastenerName, JobSettlement settlement = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            AssemblyQualityReport report = result.Report;
            var text = new StringBuilder();
            text.Append(Loc.T("JOB COMPLETE: ", "İŞ TAMAMLANDI: ")).Append(result.Job.DisplayName)
                .Append("   (").Append(FormatTime(result.Seconds)).Append(")\n");
            text.Append(AssemblyReportText.Format(result.Job.Blueprint, report, fastenerName, null,
                includeSafetyNote: false)).Append('\n');

            MaterialUsage material = result.Material;
            text.Append(Loc.T($"Material: {material.BoardsDelivered} board(s) delivered",
                $"Malzeme: {material.BoardsDelivered} tahta teslim edildi"));
            if (material.BoardsOrderedExtra > 0)
            {
                text.Append(Loc.T($" ({material.BoardsOrderedExtra} ordered extra)",
                    $" ({material.BoardsOrderedExtra} ek sipariş)"));
            }

            text.Append(Loc.T($", {Percent(material.Utilization)} in the {result.Job.Blueprint.DisplayName}\n",
                $", {Percent(material.Utilization)} yapıda kullanıldı\n"));
            text.Append(Loc.T(
                $"  in the structure {Cm3(material.InstalledCubicMeters)} cm³, sawdust {Cm3(material.SawdustCubicMeters)} cm³, offcuts {Cm3(material.OffcutCubicMeters)} cm³\n",
                $"  yapıda {Cm3(material.InstalledCubicMeters)} cm³, talaş {Cm3(material.SawdustCubicMeters)} cm³, artık {Cm3(material.OffcutCubicMeters)} cm³\n"));

            text.Append(Loc.T("Quality goals ", "Kalite hedefleri ")).Append(result.GoalsMet).Append('/')
                .Append(result.Goals.Count).Append(":\n");
            foreach (QualityGoal goal in result.Goals)
            {
                text.Append(goal.Met ? "  [x] " : "  [ ] ").Append(goal.Name).Append(" (").Append(goal.Detail).Append(")\n");
            }

            if (settlement != null)
            {
                text.Append(Settlement(settlement, result.GoalsMet, result.Goals.Count)).Append('\n');
            }

            text.Append(AssemblyReportText.SafetyNote);
            return text.ToString();
        }

        /// <summary>Pay, costs and profit of a delivered job.</summary>
        public static string Settlement(JobSettlement settlement, int goalsMet, int goalCount)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));

            var text = new StringBuilder();
            text.Append(Loc.T(
                $"Pay: {settlement.Earned} of {settlement.Pay} (delivery {settlement.BasePay} + goals {goalsMet}/{goalCount} {settlement.Bonus})\n",
                $"Ücret: {settlement.Earned} / {settlement.Pay} (teslim {settlement.BasePay} + hedefler {goalsMet}/{goalCount} {settlement.Bonus})\n"));
            text.Append(Loc.T(
                $"Material used: timber {settlement.TimberCost}, fasteners {settlement.FastenerCost}\n",
                $"Kullanılan malzeme: ahşap {settlement.TimberCost}, vida {settlement.FastenerCost}\n"));
            if (settlement.Unpriced.Count > 0)
            {
                text.Append(Loc.T("  (no price for: ", "  (fiyatı yok: ")).Append(string.Join(", ", settlement.Unpriced)).Append(")\n");
            }

            text.Append(Loc.T($"Profit: {settlement.Profit}   (offcuts stay in the workshop)",
                $"Kâr: {settlement.Profit}   (artıklar atölyede kalır)"));
            return text.ToString();
        }

        public static string FormatTime(double seconds)
        {
            int total = (int)Math.Max(0.0, Math.Floor(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private static void AppendFasteners(StringBuilder text, MaterialPlan plan, Func<string, string> fastenerName)
        {
            foreach (KeyValuePair<string, int> fastener in plan.Fasteners)
            {
                text.Append(Loc.T("Fasteners: ", "Vidalar: ")).Append(fastener.Value).Append(" x ")
                    .Append(fastenerName?.Invoke(fastener.Key) ?? fastener.Key).Append('\n');
            }
        }

        private static string SlotNames(CutListLine line)
        {
            var names = new List<string>();
            foreach (PartSlot slot in line.Slots)
            {
                if (!names.Contains(slot.DisplayName))
                {
                    names.Add(slot.DisplayName);
                }
            }

            return string.Join(", ", names);
        }

        private static string Cm3(double cubicMeters) => (cubicMeters * 1e6).ToString("0", CultureInfo.InvariantCulture);

        private static string Percent(double share) =>
            Loc.T((share * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %",
                "%" + (share * 100.0).ToString("0", CultureInfo.InvariantCulture));
    }
}
