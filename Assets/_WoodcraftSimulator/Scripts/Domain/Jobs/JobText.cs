using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Jobs
{
    /// <summary>
    /// Plain-text forms of jobs for the HUD: the job sheet (description, bill of materials, cut list with progress)
    /// and the results. ASCII/Latin-1 only (the runtime font may lack other symbols).
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
            text.Append("JOB: ").Append(job.DisplayName).Append("   (").Append(FormatTime(seconds)).Append(")\n");
            if (job.Description.Length > 0)
            {
                text.Append(job.Description).Append('\n');
            }

            text.Append("Stock: ").Append(boardsDelivered).Append(" x ").Append(plan.Stock).Append(" delivered\n");
            text.Append("Cut list:\n");
            for (int i = 0; i < plan.Lines.Count; i++)
            {
                CutListLine line = plan.Lines[i];
                text.Append("  ").Append(line.Quantity).Append(" x ").Append(line.Length).Append("  ")
                    .Append(SlotNames(line));
                if (progress != null && i < progress.Count)
                {
                    CutListLineProgress state = progress[i];
                    text.Append(state.IsDone ? "  - done" : $"  - {state.Installed} in, {state.Ready} ready");
                }

                text.Append('\n');
            }

            foreach (KeyValuePair<string, int> fastener in plan.Fasteners)
            {
                text.Append("Fasteners: ").Append(fastener.Value).Append(" x ")
                    .Append(fastenerName?.Invoke(fastener.Key) ?? fastener.Key).Append('\n');
            }

            foreach (string tip in job.Tips)
            {
                text.Append("Tip: ").Append(tip).Append('\n');
            }

            return text.ToString().TrimEnd('\n');
        }

        /// <summary>The bill of materials before accepting: stock needed, cut plan per board, expected offcuts.</summary>
        public static string Offer(Job job, MaterialPlan plan, Func<string, string> fastenerName)
        {
            var text = new StringBuilder();
            text.Append(job.DisplayName).Append('\n');
            if (job.Description.Length > 0)
            {
                text.Append(job.Description).Append('\n');
            }

            if (!plan.IsFeasible)
            {
                text.Append("Can't be made from the stock: ").Append(string.Join(" ", plan.Problems));
                return text.ToString();
            }

            text.Append("Material: ").Append(plan.BoardsNeeded).Append(" x ").Append(plan.Stock);
            if (job.SpareBoards > 0)
            {
                text.Append(" + ").Append(job.SpareBoards).Append(" spare");
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

                text.Append("  Board ").Append(i + 1).Append(": ").Append(string.Join(" + ", parts))
                    .Append(" mm, offcut ").Append(board.Offcut).Append('\n');
            }

            foreach (KeyValuePair<string, int> fastener in plan.Fasteners)
            {
                text.Append("Fasteners: ").Append(fastener.Value).Append(" x ")
                    .Append(fastenerName?.Invoke(fastener.Key) ?? fastener.Key).Append('\n');
            }

            text.Append("(kerf ").Append(plan.Kerf).Append(" per cut)");
            return text.ToString();
        }

        public static string Result(JobResult result, string fastenerName)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            AssemblyQualityReport report = result.Report;
            var text = new StringBuilder();
            text.Append("JOB COMPLETE: ").Append(result.Job.DisplayName).Append("   (").Append(FormatTime(result.Seconds)).Append(")\n");
            text.Append(AssemblyReportText.Format(result.Job.Blueprint, report, fastenerName, null,
                includeSafetyNote: false)).Append('\n');

            MaterialUsage material = result.Material;
            text.Append($"Material: {material.BoardsDelivered} board(s) delivered");
            if (material.BoardsOrderedExtra > 0)
            {
                text.Append($" ({material.BoardsOrderedExtra} ordered extra)");
            }

            text.Append($", {Percent(material.Utilization)} in the {result.Job.Blueprint.DisplayName}\n");
            text.Append($"  in the structure {Cm3(material.InstalledCubicMeters)} cm³, sawdust {Cm3(material.SawdustCubicMeters)} cm³, " +
                        $"offcuts {Cm3(material.OffcutCubicMeters)} cm³\n");

            text.Append($"Quality goals {result.GoalsMet}/{result.Goals.Count}:\n");
            foreach (QualityGoal goal in result.Goals)
            {
                text.Append(goal.Met ? "  [x] " : "  [ ] ").Append(goal.Name).Append(" (").Append(goal.Detail).Append(")\n");
            }

            text.Append(AssemblyReportText.SafetyNote);
            return text.ToString();
        }

        public static string FormatTime(double seconds)
        {
            int total = (int)Math.Max(0.0, Math.Floor(seconds));
            return $"{total / 60}:{total % 60:00}";
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

        private static string Percent(double share) => (share * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %";
    }
}
