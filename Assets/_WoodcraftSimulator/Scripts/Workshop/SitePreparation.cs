using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// Site preparation for an outdoor site (terrace Phase 10, D-046; pergola D-049): first the outline is set out with stakes and a
    /// string line (<see cref="SiteMarker"/>), then each pier spot is dug, levelled and filled with gravel
    /// (<see cref="SiteSpot"/>). A pier block only goes onto a prepared spot (the station asks <see cref="CanPlace"/>).
    /// Also names the construction stage (configurable <see cref="Stage"/>s) for the job sheet.
    /// </summary>
    public sealed class SitePreparation : MonoBehaviour
    {
        [SerializeField] private SiteSpot[] spots = Array.Empty<SiteSpot>();

        [Tooltip("The string line around the outline, shown once the site is set out.")]
        [SerializeField] private LineRenderer stringLine;

        [Tooltip("Steel brackets (visual only), each shown while the part of the same index in Bracket Slots is set " +
                 "(D-048): U beam brackets on the pier blocks, angle brackets beside the joists.")]
        [SerializeField] private GameObject[] bracketVisuals = Array.Empty<GameObject>();

        [SerializeField] private string[] bracketSlots = Array.Empty<string>();

        /// <summary>A construction stage: the parts whose slot ids start with <see cref="slotPrefix"/>.</summary>
        [Serializable]
        public sealed class Stage
        {
            public string slotPrefix = "pier";
            public string name = "foundation";
            public string nameTurkish = "temel";
        }

        [Tooltip("Construction stages after site preparation, bottom up (D-049). Default: the terrace.")]
        [SerializeField] private Stage[] stages =
        {
            new Stage { slotPrefix = "pier", name = "foundation (pier blocks)", nameTurkish = "temel (beton ayaklar)" },
            new Stage { slotPrefix = "beam", name = "beams", nameTurkish = "kirişler" },
            new Stage { slotPrefix = "joist", name = "joists", nameTurkish = "döşeme altı kirişleri" },
            new Stage { slotPrefix = "deck", name = "decking", nameTurkish = "döşeme tahtaları" }
        };

        public bool Marked { get; private set; }

        public event Action Changed;

        private AssemblyStation station;

        private void Awake() => TryGetComponent(out station);

        private void OnEnable()
        {
            if (station != null)
            {
                station.Changed += ShowBrackets;
            }
        }

        private void OnDisable()
        {
            if (station != null)
            {
                station.Changed -= ShowBrackets;
            }
        }

        private void Start() => Refresh();

        /// <summary>
        /// Each steel bracket shows while its part is set (D-048): the U bracket with its pier block (the beam then
        /// sits in it), the angle bracket with its joist (then screwed through its upright leg).
        /// </summary>
        private void ShowBrackets()
        {
            AssemblyBuild build = station != null ? station.Build : null;
            for (int i = 0; i < bracketVisuals.Length && i < bracketSlots.Length; i++)
            {
                if (bracketVisuals[i] != null)
                {
                    bracketVisuals[i].SetActive(build != null && build.GetPiece(bracketSlots[i]) != null);
                }
            }
        }

        public void Mark()
        {
            Marked = true;
            Refresh();
        }

        public void Prepare(SiteSpot spot)
        {
            if (Marked && spot != null)
            {
                spot.Prepared = true;
                Refresh();
            }
        }

        /// <summary>Whether a part may go into the slot: a pier needs its prepared spot (other slots are not gated).</summary>
        public bool CanPlace(string slotId, out string reason)
        {
            reason = null;
            foreach (SiteSpot spot in spots)
            {
                if (spot == null || spot.SlotId != slotId || spot.Prepared)
                {
                    continue;
                }

                reason = Marked
                    ? Loc.T("prepare this spot first: dig, level and fill with gravel (E on the spot)",
                        "önce bu yeri hazırla: kaz, düzle, çakıl dök (yerde E)")
                    : Loc.T("set out the site first: stakes and string line (E on the corner stake)",
                        "önce alanı işaretle: kazık ve ip (köşe kazığında E)");
                return false;
            }

            // A part of a later stage only after the customer passed the earlier ones (D-053).
            int stage = StageIndexOf(slotId);
            AssemblyBuild build = station != null ? station.Build : null;
            for (int earlier = 0; build != null && earlier < stage; earlier++)
            {
                if (NeedsInspection(build, earlier) && !Inspections.IsPassed(stages[earlier].slotPrefix))
                {
                    reason = Loc.T($"the customer inspects the {stages[earlier].name} first (E on the corner stake)",
                        $"önce müşteri {stages[earlier].nameTurkish} aşamasını denetlemeli (köşe kazığında E)");
                    return false;
                }
            }

            return true;
        }

        /// <summary>Stages passed by the customer and first-time results (D-053).</summary>
        public InspectionLog Inspections { get; } = new InspectionLog();

        /// <summary>The result of the last inspection asked for, until the next one (for the job card).</summary>
        public InspectionResult LastInspection { get; private set; }

        /// <summary>Stage (index into the stages) a slot belongs to, or -1.</summary>
        public int StageIndexOf(string slotId)
        {
            for (int s = 0; s < stages.Length; s++)
            {
                if (slotId != null && slotId.StartsWith(stages[s].slotPrefix, StringComparison.Ordinal))
                {
                    return s;
                }
            }

            return -1;
        }

        /// <summary>
        /// Whether the customer inspects this stage before the next one: every stage with joints except the last
        /// (the job's own completion report covers that one).
        /// </summary>
        public bool NeedsInspection(AssemblyBuild build, int stage) =>
            stage >= 0 && stage < stages.Length - 1 && StageInspection.HasChecks(build.Blueprint, stage, StageIndexOf);

        /// <summary>Stages the customer inspects in this build (for the pay goal).</summary>
        public int InspectedStageCount(AssemblyBuild build)
        {
            int count = 0;
            for (int s = 0; s < stages.Length; s++)
            {
                if (NeedsInspection(build, s))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The first stage still waiting for the customer's approval, or -1.</summary>
        public int PendingInspection(AssemblyBuild build)
        {
            for (int s = 0; s < stages.Length; s++)
            {
                if (NeedsInspection(build, s) && !Inspections.IsPassed(stages[s].slotPrefix))
                {
                    return s;
                }
            }

            return -1;
        }

        public string StageName(int stage) => Loc.T(stages[stage].name, stages[stage].nameTurkish);

        /// <summary>
        /// The customer inspects the pending stage (E on the corner stake): its parts must all be set. Returns what he
        /// says. A failed inspection can be asked for again once fixed; only the first one counts for "first time".
        /// </summary>
        public string RequestInspection()
        {
            AssemblyBuild build = station != null ? station.Build : null;
            int stage = build != null ? PendingInspection(build) : -1;
            if (stage < 0)
            {
                return Loc.T("Nothing to inspect: every stage so far is approved", "Denetlenecek aşama yok: şimdiye kadarkiler onaylı");
            }

            string name = StageName(stage);
            if (!StageInspection.AllPartsSet(build, stage, StageIndexOf))
            {
                return Loc.T($"The {name} are not finished yet: set every part first", $"{Capital(name)} henüz bitmedi: önce tüm parçaları tak");
            }

            InspectionResult result = StageInspection.Inspect(build, stage, StageIndexOf);
            Inspections.Record(stages[stage].slotPrefix, result);
            LastInspection = result;
            Changed?.Invoke();

            if (result.Passed)
            {
                string notes = result.Notes.Count > 0
                    ? Loc.T($" ({result.Notes.Count} flaw(s) noted: {result.Notes[0]})", $" ({result.Notes.Count} kusur not edildi: {result.Notes[0]})")
                    : string.Empty;
                return Loc.T($"Customer: the {name} are approved, go on{notes}", $"Müşteri: {name} onaylandı, devam{notes}");
            }

            string more = result.Problems.Count > 1 ? Loc.T($" (+{result.Problems.Count - 1} more)", $" (+{result.Problems.Count - 1} tane daha)") : string.Empty;
            return Loc.T($"Customer: {result.Problems.Count} problem(s) to fix: {result.Problems[0]}{more}",
                $"Müşteri: {result.Problems.Count} sorun düzeltilmeli: {result.Problems[0]}{more}");
        }

        private static string Capital(string text) =>
            string.IsNullOrEmpty(text) ? text : (Loc.IsTurkish && text[0] == 'i' ? 'İ' : char.ToUpperInvariant(text[0])) + text.Substring(1);

        /// <summary>Where the work stands: stage number of count, its name and progress (for the job card).</summary>
        public readonly struct StageState
        {
            public StageState(int number, int count, string name, int done, int total, string note = null)
            {
                Number = number;
                Count = count;
                Name = name;
                Done = done;
                Total = total;
                Note = note;
            }

            /// <summary>1-based; <see cref="Count"/> + 1 once every stage is done.</summary>
            public int Number { get; }

            public int Count { get; }

            public string Name { get; }

            public int Done { get; }

            public int Total { get; }

            /// <summary>What to do first within the stage (e.g. set out the site), or null.</summary>
            public string Note { get; }

            public bool AllDone => Number > Count;
        }

        /// <summary>
        /// The construction stage from the work done: site preparation, then one stage per part group (the last one
        /// includes fastening whatever is still loose).
        /// </summary>
        public StageState CurrentStage(AssemblyBuild build)
        {
            int prepared = 0;
            foreach (SiteSpot spot in spots)
            {
                if (spot != null && spot.Prepared)
                {
                    prepared++;
                }
            }

            // Fastening whatever is still loose counts with the last part stage.
            int count = stages.Length + 1;
            if (!Marked || prepared < spots.Length)
            {
                return new StageState(1, count, Loc.T("site preparation", "zemin hazırlığı"), prepared, spots.Length,
                    Marked ? null : Loc.T("set out the site first (corner stake)", "önce alanı işaretle (köşe kazığı)"));
            }

            for (int s = 0; s < stages.Length; s++)
            {
                int total = 0;
                int installed = 0;
                foreach (PartSlot slot in build.Blueprint.Slots)
                {
                    if (slot.Id.StartsWith(stages[s].slotPrefix, StringComparison.Ordinal))
                    {
                        total++;
                        if (build.GetPiece(slot.Id) != null)
                        {
                            installed++;
                        }
                    }
                }

                if (installed < total)
                {
                    return new StageState(s + 2, count, Loc.T(stages[s].name, stages[s].nameTurkish), installed, total);
                }

                // All set: the stage is done once the customer approved it (D-053).
                if (NeedsInspection(build, s) && !Inspections.IsPassed(stages[s].slotPrefix))
                {
                    string note = Inspections.WasRequested(stages[s].slotPrefix) && LastInspection != null && !LastInspection.Passed
                        ? Loc.T($"Inspection: {LastInspection.Problems.Count} problem(s): {string.Join("; ", LastInspection.Problems)}",
                            $"Denetim: {LastInspection.Problems.Count} sorun: {string.Join("; ", LastInspection.Problems)}")
                        : Loc.T("Ready: ask for the inspection (E on the corner stake)", "Hazır: denetim iste (köşe kazığında E)");
                    return new StageState(s + 2, count, Loc.T(stages[s].name, stages[s].nameTurkish), installed, total, note);
                }
            }

            AssemblyQualityReport report = build.CreateReport();
            return report.IsComplete
                ? new StageState(count + 1, count, Loc.T("all stages done", "tüm aşamalar bitti"), 1, 1)
                : new StageState(count, count, Loc.T("fasten everything", "her şeyi sabitle"), report.FastenersInstalled,
                    report.FastenersRequired);
        }

        private const string PassedPrefix = "passed:";
        private const string FailedFirstPrefix = "failed-first:";

        /// <summary>
        /// Save state: "marked", the slot ids of the prepared spots, and the inspections ("passed:post",
        /// "failed-first:post"; D-053).
        /// </summary>
        public string[] Capture()
        {
            var done = new List<string>();
            if (Marked)
            {
                done.Add("marked");
            }

            foreach (SiteSpot spot in spots)
            {
                if (spot != null && spot.Prepared)
                {
                    done.Add(spot.SlotId);
                }
            }

            // Inspections (D-053) in the same list, by prefix (the save format needs no new field).
            foreach (string stage in Inspections.Passed)
            {
                done.Add(PassedPrefix + stage);
            }

            foreach (string stage in Inspections.FailedFirst)
            {
                done.Add(FailedFirstPrefix + stage);
            }

            return done.ToArray();
        }

        public void Restore(IEnumerable<string> done)
        {
            var set = new HashSet<string>(done ?? Array.Empty<string>());
            Marked = set.Contains("marked");
            foreach (SiteSpot spot in spots)
            {
                if (spot != null)
                {
                    spot.Prepared = set.Contains(spot.SlotId);
                }
            }

            var passed = new List<string>();
            var failedFirst = new List<string>();
            foreach (string entry in set)
            {
                if (entry.StartsWith(PassedPrefix, StringComparison.Ordinal))
                {
                    passed.Add(entry.Substring(PassedPrefix.Length));
                }
                else if (entry.StartsWith(FailedFirstPrefix, StringComparison.Ordinal))
                {
                    failedFirst.Add(entry.Substring(FailedFirstPrefix.Length));
                }
            }

            Inspections.Restore(passed, failedFirst);
            LastInspection = null;

            Refresh();
        }

        private void Refresh()
        {
            if (stringLine != null)
            {
                stringLine.enabled = Marked;
            }

            foreach (SiteSpot spot in spots)
            {
                if (spot != null)
                {
                    spot.ShowState(Marked);
                }
            }

            ShowBrackets();

            Changed?.Invoke();
        }
    }
}
