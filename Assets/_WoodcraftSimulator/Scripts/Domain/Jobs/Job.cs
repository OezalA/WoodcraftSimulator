using System;
using System.Collections.Generic;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Jobs
{
    /// <summary>
    /// A woodworking assignment (master prompt §6.14): what to build, from which stock, the pay, how many spare boards
    /// the plan recommends, and tips. Objectives: cut the parts, assemble, fasten; quality goals are reported
    /// separately (<see cref="JobResult"/>), the money in <see cref="JobSettlement"/>.
    /// </summary>
    public sealed class Job
    {
        public Job(string id, string displayName, string description, Blueprint blueprint, StockSpec stock,
            int spareBoards = 0, IEnumerable<string> tips = null, Money pay = default, bool requiresFinish = false)
        {
            RequiresFinish = requiresFinish;
            if (pay.IsNegative)
            {
                throw new ArgumentOutOfRangeException(nameof(pay), "Pay must not be negative.");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Job id must not be empty.", nameof(id));
            }

            if (spareBoards < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spareBoards), spareBoards, "Spare boards must not be negative.");
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Description = description ?? string.Empty;
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Stock = stock ?? throw new ArgumentNullException(nameof(stock));
            SpareBoards = spareBoards;
            Tips = new List<string>(tips ?? Array.Empty<string>()).AsReadOnly();
            Pay = pay;
        }

        /// <summary>Full pay when every quality goal is met (see <see cref="JobSettlement"/>).</summary>
        public Money Pay { get; }

        /// <summary>The customer wants every part sanded and oiled (an extra quality goal, D-045).</summary>
        public bool RequiresFinish { get; }

        public string Id { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public Blueprint Blueprint { get; }

        public StockSpec Stock { get; }

        public int SpareBoards { get; }

        public IReadOnlyList<string> Tips { get; }

        public MaterialPlan Plan(Length kerf) => MaterialPlan.Calculate(Blueprint, Stock, kerf);

        /// <summary>Boards delivered when the job is accepted: what the plan needs plus the spares.</summary>
        public int BoardsToDeliver(Length kerf) => Plan(kerf).BoardsNeeded + SpareBoards;
    }

    /// <summary>Progress of one cut list line: parts installed, and fitting pieces lying ready.</summary>
    public readonly struct CutListLineProgress
    {
        public CutListLineProgress(CutListLine line, int installed, int ready)
        {
            Line = line;
            Installed = installed;
            Ready = ready;
        }

        public CutListLine Line { get; }

        public int Installed { get; }

        /// <summary>Cut pieces that fit this line and are not installed yet (capped at what is still needed).</summary>
        public int Ready { get; }

        public int Missing => Math.Max(0, Line.Quantity - Installed - Ready);

        public bool IsDone => Installed >= Line.Quantity;
    }

    public static class CutListProgress
    {
        /// <summary>
        /// For each cut list line: how many parts are installed in the assembly and how many loose pieces fit it (within
        /// the blueprint's fit tolerance). A loose piece is counted for one line only.
        /// </summary>
        public static IReadOnlyList<CutListLineProgress> Evaluate(MaterialPlan plan, AssemblyBuild build,
            IEnumerable<TimberPiece> loosePieces)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (build == null) throw new ArgumentNullException(nameof(build));

            var unused = new List<TimberPiece>(loosePieces ?? Array.Empty<TimberPiece>());
            var result = new List<CutListLineProgress>();
            foreach (CutListLine line in plan.Lines)
            {
                int installed = 0;
                foreach (PartSlot slot in line.Slots)
                {
                    if (build.GetPiece(slot.Id) != null)
                    {
                        installed++;
                    }
                }

                int ready = 0;
                int needed = line.Quantity - installed;
                PartSlot reference = line.Slots[0];
                for (int i = unused.Count - 1; i >= 0 && ready < needed; i--)
                {
                    if (build.CheckFit(reference, unused[i]).Fits)
                    {
                        ready++;
                        unused.RemoveAt(i);
                    }
                }

                result.Add(new CutListLineProgress(line, installed, ready));
            }

            return result.AsReadOnly();
        }
    }

    /// <summary>Material of a job: what was delivered and ordered, what went into the structure, what became dust.</summary>
    public readonly struct MaterialUsage
    {
        public MaterialUsage(int boardsDelivered, int boardsOrderedExtra, double deliveredCubicMeters,
            double installedCubicMeters, double sawdustCubicMeters)
        {
            BoardsDelivered = boardsDelivered;
            BoardsOrderedExtra = boardsOrderedExtra;
            DeliveredCubicMeters = deliveredCubicMeters;
            InstalledCubicMeters = installedCubicMeters;
            SawdustCubicMeters = sawdustCubicMeters;
        }

        /// <summary>Boards delivered with the job, including extra ones ordered later.</summary>
        public int BoardsDelivered { get; }

        public int BoardsOrderedExtra { get; }

        public double DeliveredCubicMeters { get; }

        public double InstalledCubicMeters { get; }

        public double SawdustCubicMeters { get; }

        /// <summary>Delivered material that is neither in the structure nor sawdust: offcuts and rejects.</summary>
        public double OffcutCubicMeters => Math.Max(0.0, DeliveredCubicMeters - InstalledCubicMeters - SawdustCubicMeters);

        /// <summary>Share of the delivered material that went into the structure (0..1), or 0 if nothing was delivered.</summary>
        public double Utilization => DeliveredCubicMeters > 0.0 ? Math.Min(1.0, InstalledCubicMeters / DeliveredCubicMeters) : 0.0;
    }

    /// <summary>One optional quality goal and whether it was met.</summary>
    public readonly struct QualityGoal
    {
        public QualityGoal(string name, bool met, string detail)
        {
            Name = name;
            Met = met;
            Detail = detail;
        }

        public string Name { get; }

        public bool Met { get; }

        public string Detail { get; }
    }

    /// <summary>
    /// The outcome of a finished job, as separate explained components (master prompt §6.12): the assembly report,
    /// material usage, time and the quality goals. No single score; nothing here certifies structural safety.
    /// </summary>
    public sealed class JobResult
    {
        private JobResult(Job job, AssemblyQualityReport report, MaterialUsage material, double seconds,
            IReadOnlyList<QualityGoal> goals)
        {
            Job = job;
            Report = report;
            Material = material;
            Seconds = seconds;
            Goals = goals;
        }

        public Job Job { get; }

        public AssemblyQualityReport Report { get; }

        public MaterialUsage Material { get; }

        /// <summary>Playing time from accepting the job to completing it.</summary>
        public double Seconds { get; }

        public IReadOnlyList<QualityGoal> Goals { get; }

        public int GoalsMet
        {
            get
            {
                int met = 0;
                foreach (QualityGoal goal in Goals)
                {
                    if (goal.Met)
                    {
                        met++;
                    }
                }

                return met;
            }
        }

        /// <param name="finishedParts">Installed parts that are sanded and oiled (only counted if the job asks for it).</param>
        public static JobResult Create(Job job, AssemblyQualityReport report, MaterialUsage material, double seconds,
            int finishedParts = 0)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (seconds < 0.0) throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Time must not be negative.");

            var goals = new List<QualityGoal>
            {
                new QualityGoal(
                    Loc.T($"Every part within {job.Blueprint.AccuracyTolerance}", $"Her parça {job.Blueprint.AccuracyTolerance} içinde"),
                    report.AccurateParts == report.PartsRequired,
                    Loc.T($"{report.AccurateParts}/{report.PartsRequired} parts", $"{report.AccurateParts}/{report.PartsRequired} parça")),
                new QualityGoal(Loc.T("Every screw head flush", "Her vida başı hizada"),
                    report.FlushFasteners == report.FastenersRequired,
                    Loc.T($"{report.FlushFasteners}/{report.FastenersRequired} flush", $"{report.FlushFasteners}/{report.FastenersRequired} hizada")),
                new QualityGoal(Loc.T("No split boards", "Çatlak tahta yok"),
                    report.Splits == 0,
                    report.Splits == 0 ? Loc.T("none", "yok") : Loc.T($"{report.Splits} split", $"{report.Splits} çatlak")),
                new QualityGoal(Loc.T("No extra stock ordered", "Ek tahta sipariş edilmedi"),
                    material.BoardsOrderedExtra == 0,
                    material.BoardsOrderedExtra == 0
                        ? Loc.T("none", "yok")
                        : Loc.T($"{material.BoardsOrderedExtra} extra board(s)", $"{material.BoardsOrderedExtra} ek tahta"))
            };

            if (job.RequiresFinish)
            {
                goals.Add(new QualityGoal(Loc.T("Every part sanded and oiled", "Her parça zımparalı ve yağlı"),
                    finishedParts >= report.PartsRequired,
                    Loc.T($"{finishedParts}/{report.PartsRequired} parts", $"{finishedParts}/{report.PartsRequired} parça")));
            }

            return new JobResult(job, report, material, seconds, goals.AsReadOnly());
        }
    }
}
