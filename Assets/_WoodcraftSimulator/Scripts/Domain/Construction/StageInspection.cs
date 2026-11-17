using System;
using System.Collections.Generic;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Construction
{
    /// <summary>
    /// The customer's check of one construction stage (Phase 11e, D-053). <see cref="Problems"/> can be fixed and block
    /// the next stage (a joint not fastened yet, a screw head standing out, glue still wet); <see cref="Notes"/> cannot
    /// be fixed any more and only cost money through the job's quality goals (a part outside the accuracy tolerance,
    /// a split board, a screw driven too deep).
    /// </summary>
    public sealed class InspectionResult
    {
        public InspectionResult(IReadOnlyList<string> problems, IReadOnlyList<string> notes)
        {
            Problems = problems ?? Array.Empty<string>();
            Notes = notes ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Problems { get; }

        public IReadOnlyList<string> Notes { get; }

        public bool Passed => Problems.Count == 0;
    }

    /// <summary>
    /// What belongs to a stage and how it is checked. Stages are numbered 0.. bottom up; a slot's stage comes from
    /// <c>stageOf</c> (-1 = none). A connection belongs to the stage of the later of its two parts (the beam-to-post
    /// bracket is part of the beams, it can only be fixed once the beam is there).
    /// </summary>
    public static class StageInspection
    {
        public static int StageOf(ConnectionSpec connection, Func<string, int> stageOf) =>
            Math.Max(stageOf(connection.PartA), stageOf(connection.PartB));

        /// <summary>Whether the stage has anything to check (connections); a stage without joints passes by itself.</summary>
        public static bool HasChecks(Blueprint blueprint, int stage, Func<string, int> stageOf)
        {
            foreach (ConnectionSpec connection in blueprint.Connections)
            {
                if (StageOf(connection, stageOf) == stage)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether every part of the stage is set (only then can it be inspected).</summary>
        public static bool AllPartsSet(AssemblyBuild build, int stage, Func<string, int> stageOf)
        {
            foreach (PartSlot slot in build.Blueprint.Slots)
            {
                if (stageOf(slot.Id) == stage && build.GetPiece(slot.Id) == null)
                {
                    return false;
                }
            }

            return true;
        }

        public static InspectionResult Inspect(AssemblyBuild build, int stage, Func<string, int> stageOf)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            if (stageOf == null) throw new ArgumentNullException(nameof(stageOf));

            var problems = new List<string>();
            var notes = new List<string>();
            Blueprint blueprint = build.Blueprint;
            foreach (PartSlot slot in blueprint.Slots)
            {
                if (stageOf(slot.Id) != stage)
                {
                    continue;
                }

                TimberPiece piece = build.GetPiece(slot.Id);
                if (piece == null)
                {
                    problems.Add(Loc.T($"{slot.DisplayName}: not set", $"{slot.DisplayName}: takılmadı"));
                }
                else if (!build.CheckFit(slot, piece).IsAccurate)
                {
                    notes.Add(Loc.T($"{slot.DisplayName}: outside {blueprint.AccuracyTolerance}",
                        $"{slot.DisplayName}: {blueprint.AccuracyTolerance} dışında"));
                }
            }

            foreach (ConnectionSpec connection in blueprint.Connections)
            {
                if (StageOf(connection, stageOf) != stage)
                {
                    continue;
                }

                string name = $"{SlotName(blueprint, connection.PartA)} - {SlotName(blueprint, connection.PartB)}";
                int missing = 0;
                int proud = 0;
                int deep = 0;
                int splits = 0;
                for (int point = 0; point < connection.FastenerCount; point++)
                {
                    if (!build.IsFastened(connection.Id, point))
                    {
                        missing++;
                        continue;
                    }

                    ScrewSeat seat = FasteningRules.Seat(build.HeadHeight(connection.Id, point) ?? default);
                    if (seat == ScrewSeat.Proud)
                    {
                        proud++;
                    }
                    else if (seat == ScrewSeat.Overdriven)
                    {
                        deep++;
                    }

                    if (build.IsSplit(connection.Id, point))
                    {
                        splits++;
                    }
                }

                if (missing > 0)
                {
                    problems.Add(Loc.T($"{name}: {missing}/{connection.FastenerCount} not fastened",
                        $"{name}: {missing}/{connection.FastenerCount} sabitlenmedi"));
                }

                if (proud > 0)
                {
                    problems.Add(Loc.T($"{name}: {proud} screw head(s) stand out", $"{name}: {proud} vida başı taşıyor"));
                }

                if (build.IsDrying(connection.Id))
                {
                    problems.Add(Loc.T($"{name}: glue still drying", $"{name}: tutkal henüz kurumadı"));
                }

                if (deep > 0)
                {
                    notes.Add(Loc.T($"{name}: {deep} screw(s) driven too deep", $"{name}: {deep} vida fazla gömülmüş"));
                }

                if (splits > 0)
                {
                    notes.Add(Loc.T($"{name}: {splits} split(s)", $"{name}: {splits} çatlak"));
                }
            }

            return new InspectionResult(problems.AsReadOnly(), notes.AsReadOnly());
        }

        private static string SlotName(Blueprint blueprint, string slotId) =>
            blueprint.TryGetSlot(slotId, out PartSlot slot) ? slot.DisplayName : slotId;
    }

    /// <summary>
    /// Which stages the customer has passed, and which failed their first inspection (for pay, D-053). Stages are
    /// named by id (the site's stage prefix) so the record survives saving.
    /// </summary>
    public sealed class InspectionLog
    {
        private readonly HashSet<string> passed = new HashSet<string>();
        private readonly HashSet<string> failedFirst = new HashSet<string>();
        private readonly HashSet<string> requested = new HashSet<string>();

        public bool IsPassed(string stageId) => passed.Contains(stageId ?? string.Empty);

        public bool WasRequested(string stageId) => requested.Contains(stageId ?? string.Empty);

        public IEnumerable<string> Passed => passed;

        public IEnumerable<string> FailedFirst => failedFirst;

        /// <summary>Stages inspected at least once.</summary>
        public int Inspected => requested.Count;

        /// <summary>Stages that passed at their first inspection.</summary>
        public int PassedFirstTime
        {
            get
            {
                int count = 0;
                foreach (string stage in passed)
                {
                    if (!failedFirst.Contains(stage))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Records an inspection: the first one of a stage decides "first time".</summary>
        public void Record(string stageId, InspectionResult result)
        {
            if (string.IsNullOrEmpty(stageId) || result == null)
            {
                return;
            }

            if (requested.Add(stageId) && !result.Passed)
            {
                failedFirst.Add(stageId);
            }

            if (result.Passed)
            {
                passed.Add(stageId);
            }
        }

        public void Restore(IEnumerable<string> passedStages, IEnumerable<string> failedFirstStages)
        {
            passed.Clear();
            failedFirst.Clear();
            requested.Clear();
            foreach (string stage in passedStages ?? Array.Empty<string>())
            {
                passed.Add(stage);
                requested.Add(stage);
            }

            foreach (string stage in failedFirstStages ?? Array.Empty<string>())
            {
                failedFirst.Add(stage);
                requested.Add(stage);
            }
        }

        public void Clear() => Restore(null, null);
    }
}
