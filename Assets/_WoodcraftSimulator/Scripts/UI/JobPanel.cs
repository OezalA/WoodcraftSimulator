using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Localization;
using MeasuredLength = WoodcraftSimulator.Measurement.Length;
using WoodcraftSimulator.Workshop;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// HUD part for jobs (UI Toolkit, same document as <see cref="GameplayHud"/>). Top left: the job card of the active
    /// job (D-051: header with time, stage with progress bar, cut list as a table, material, tips), or the offer card
    /// of the job sheet being looked at (D-062: pay, money, what to buy, cut plan); on the right the result card once
    /// the structure is complete (D-062: goals, parts, material, pay). The active card's elements are built once per job
    /// (and language) and only their texts and bars are updated, a few times per second (the cut list counts the
    /// pieces lying around); the offer and result cards are rebuilt only when their content changes. Styling:
    /// UI/Hud.uss, classes <c>job-card*</c>, <c>cut-row*</c> and <c>value-row*</c>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class JobPanel : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        /// <summary>Rows of a cut plan or of part problems shown at most; the rest is counted in one line.</summary>
        private const int MaxRows = 8;

        [SerializeField] private JobBoard jobBoard;
        [SerializeField] private PlayerInteractor interactor;

        [Tooltip("Seconds between refreshes of the job card.")]
        [SerializeField, Min(0.05f)] private float refreshSeconds = 0.5f;

        private VisualElement card;
        private VisualElement offerCard;
        private VisualElement resultCard;
        private float nextRefresh;

        // What the offer and result cards show now (their text form), to rebuild them only when it changes.
        private string builtOffer;
        private string builtResult;

        // The card as built for this job and language.
        private Job builtJob;
        private Language builtLanguage;
        private Label timeLabel;
        private VisualElement stageSection;
        private Label stageNumberLabel;
        private Label stageCountLabel;
        private Label stageNameLabel;
        private Label stageNoteLabel;
        private VisualElement stageFill;
        private Label cutSummaryLabel;
        private Label stockLabel;
        private readonly List<CutRow> cutRows = new List<CutRow>();

        private sealed class CutRow
        {
            public VisualElement Dot;
            public Label Status;
        }

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            offerCard = root.Q<VisualElement>("offer-card");
            resultCard = root.Q<VisualElement>("result-card");
            card = root.Q<VisualElement>("job-card");
            if (offerCard == null || resultCard == null || card == null || jobBoard == null || interactor == null)
            {
                Debug.LogError($"{nameof(JobPanel)}: needs 'job-card', 'offer-card' and 'result-card' in the UXML, a Job " +
                               "Board and an Interactor.", this);
                enabled = false;
                return;
            }

            jobBoard.Changed += Refresh;
            interactor.TargetChanged += OnTargetChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (jobBoard != null)
            {
                jobBoard.Changed -= Refresh;
            }

            if (interactor != null)
            {
                interactor.TargetChanged -= OnTargetChanged;
            }
        }

        private void OnTargetChanged(IInteractable target) => Refresh();

        private void Update()
        {
            if (Time.unscaledTime >= nextRefresh)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + refreshSeconds;

            Job job = jobBoard.ActiveJob;
            if (job != null)
            {
                if (job != builtJob || Loc.Current != builtLanguage)
                {
                    BuildCard(job, jobBoard.ActivePlan);
                }

                UpdateCard();
                card.RemoveFromClassList(HiddenClass);
                ShowOffer(-1);
            }
            else
            {
                card.AddToClassList(HiddenClass);
                builtJob = null;
                ShowOffer(interactor.CurrentTarget is JobSheet jobSheet && jobSheet.Board == jobBoard ? jobSheet.JobIndex : -1);
            }

            ShowResult(jobBoard.Result);
        }

        private void BuildCard(Job job, MaterialPlan plan)
        {
            builtJob = job;
            builtLanguage = Loc.Current;
            card.Clear();
            cutRows.Clear();

            // Header: what this is, the time; then the job's name and one line about it.
            VisualElement header = Add(card, new VisualElement(), "job-card__header");
            Add(header, new Label(Loc.T("ACTIVE JOB", "AKTİF İŞ") + PlaceSuffix(jobBoard.ActiveJobPlace)), "job-card__eyebrow");
            timeLabel = Add(header, new Label(), "job-card__time");
            Add(card, new Label(job.DisplayName), "job-card__title");
            if (job.Description.Length > 0)
            {
                Add(card, new Label(job.Description), "job-card__description");
            }

            // Outdoor jobs: the construction stage with a progress bar (D-046, D-049).
            stageSection = Section(card, Loc.T("STAGE", "AŞAMA"), out stageNumberLabel, out stageCountLabel);
            stageNameLabel = Add(stageSection, new Label(), "job-card__stage-name");
            VisualElement bar = Add(stageSection, new VisualElement(), "job-card__bar");
            stageFill = Add(bar, new VisualElement(), "job-card__bar-fill");
            stageNoteLabel = Add(stageSection, new Label(), "job-card__note");

            // Cut list as a table: status, quantity, size, parts, progress.
            VisualElement cutSection = Section(card, Loc.T("CUT LIST", "KESİM LİSTESİ"), out _, out cutSummaryLabel);
            foreach (CutListLine line in plan.Lines)
            {
                VisualElement row = Add(cutSection, new VisualElement(), "cut-row");
                var cutRow = new CutRow { Dot = Add(row, new VisualElement(), "cut-row__dot") };
                Add(row, new Label($"{line.Quantity} ×"), "cut-row__qty");
                Add(row, new Label(line.Dimensions.ToString()), "cut-row__size");
                Add(row, new Label(SlotNames(line)), "cut-row__name");
                cutRow.Status = Add(row, new Label(), "cut-row__status");
                cutRows.Add(cutRow);
            }

            // Material: the stock set aside for the job and the fasteners it takes.
            VisualElement material = Section(card, Loc.T("MATERIAL", "MALZEME"), out _, out _);
            foreach (Materials.StockSpec stock in plan.Stocks)
            {
                Add(material, new Label($"{plan.BoardsNeededFor(stock)} × {stock.SpeciesName} {stock.Dimensions}"), "job-card__line");
            }

            stockLabel = Add(material, new Label(), "job-card__line");
            foreach (KeyValuePair<string, int> fastener in plan.Fasteners)
            {
                Add(material, new Label($"{fastener.Value} × {jobBoard.FastenerName(fastener.Key)}"), "job-card__line");
            }

            if (job.Tips.Count > 0)
            {
                VisualElement tips = Section(card, Loc.T("TIPS", "İPUÇLARI"), out _, out _);
                foreach (string tip in job.Tips)
                {
                    VisualElement row = Add(tips, new VisualElement(), "job-card__tip");
                    Add(row, new VisualElement(), "job-card__tip-mark");
                    Add(row, new Label(tip), "job-card__tip-text");
                }
            }
        }

        private void UpdateCard()
        {
            timeLabel.text = JobText.FormatTime(jobBoard.ElapsedSeconds);

            bool hasStage = jobBoard.TryGetStage(out SitePreparation.StageState stage);
            stageSection.EnableInClassList(HiddenClass, !hasStage);
            if (hasStage)
            {
                stageNumberLabel.text = stage.AllDone ? string.Empty : $"{stage.Number}/{stage.Count}";
                stageCountLabel.text = stage.Total > 0 ? $"{stage.Done}/{stage.Total}" : string.Empty;
                stageNameLabel.text = Capitalized(stage.Name);
                float share = stage.Total > 0 ? Mathf.Clamp01((float)stage.Done / stage.Total) : 0f;
                stageFill.style.width = Length.Percent(share * 100f);
                stageFill.EnableInClassList("job-card__bar-fill--done", stage.AllDone);
                stageNoteLabel.text = stage.Note ?? string.Empty;
                stageNoteLabel.EnableInClassList(HiddenClass, stage.Note == null);
            }

            IReadOnlyList<CutListLineProgress> progress = jobBoard.ActiveProgress();
            int installed = 0;
            int required = 0;
            for (int i = 0; i < cutRows.Count && i < progress.Count; i++)
            {
                CutListLineProgress state = progress[i];
                installed += Mathf.Min(state.Installed, state.Line.Quantity);
                required += state.Line.Quantity;

                bool started = state.Installed > 0 || state.Ready > 0;
                cutRows[i].Dot.EnableInClassList("cut-row__dot--done", state.IsDone);
                cutRows[i].Dot.EnableInClassList("cut-row__dot--started", !state.IsDone && started);
                cutRows[i].Status.EnableInClassList("cut-row__status--done", state.IsDone);
                cutRows[i].Status.text = state.IsDone
                    ? Loc.T("done", "tamam")
                    : state.Ready > 0
                        ? Loc.T($"{state.Installed}/{state.Line.Quantity} in, {state.Ready} ready",
                            $"{state.Installed}/{state.Line.Quantity} takılı, {state.Ready} hazır")
                        : Loc.T($"{state.Installed}/{state.Line.Quantity} in", $"{state.Installed}/{state.Line.Quantity} takılı");
            }

            cutSummaryLabel.text = Loc.T($"{installed}/{required} in", $"{installed}/{required} takılı");
            // Boards ordered on top of the plan (a miscut, D-040).
            int extra = jobBoard.BoardsDelivered - jobBoard.ActivePlan.BoardsNeeded;
            stockLabel.text = extra > 0 ? Loc.T($"+ {extra} ordered extra", $"+ {extra} ek sipariş") : string.Empty;
            stockLabel.EnableInClassList(HiddenClass, extra <= 0);
        }

        // ---- Offer card (D-062) ----

        /// <summary>The offer card of a sheet, or hidden for -1 / an empty sheet.</summary>
        private void ShowOffer(int index)
        {
            string offerText = index >= 0 ? jobBoard.OfferText(index) : null;
            if (offerText == null)
            {
                offerCard.AddToClassList(HiddenClass);
                builtOffer = null;
                return;
            }

            // The text form covers the job, plan and shopping; money and the lock can change on their own.
            string key = $"{index}|{Loc.Current}|{jobBoard.Balance}|{jobBoard.LockReasonAt(index)}|{offerText}";
            if (key != builtOffer)
            {
                builtOffer = key;
                BuildOffer(index);
            }

            offerCard.RemoveFromClassList(HiddenClass);
        }

        private void BuildOffer(int index)
        {
            offerCard.Clear();
            Job job = jobBoard.JobAt(index);
            MaterialPlan plan = jobBoard.PlanAt(index);
            JobOffer offer = jobBoard.OfferAt(index);

            VisualElement header = Add(offerCard, new VisualElement(), "job-card__header");
            Add(header, new Label(Loc.T("JOB OFFER", "İŞ TEKLİFİ") + PlaceSuffix(jobBoard.PlaceAt(index))), "job-card__eyebrow");
            if (job.Pay > Money.Zero)
            {
                Add(header, new Label(job.Pay.ToString()), "job-card__pay");
            }

            Add(offerCard, new Label(job.DisplayName), "job-card__title");
            if (offer != null && !string.IsNullOrEmpty(offer.Customer))
            {
                Add(offerCard, new Label(Loc.T("Customer: ", "Müşteri: ") + offer.Customer), "job-card__customer");
            }

            if (job.Description.Length > 0)
            {
                Add(offerCard, new Label(job.Description), "job-card__description");
            }

            string locked = jobBoard.LockReasonAt(index);
            if (locked != null)
            {
                Add(offerCard, new Label(Loc.T("Not yet: ", "Henüz değil: ") + locked), "job-card__warning");
                return;
            }

            if (!plan.IsFeasible)
            {
                Add(offerCard, new Label(Loc.T("Can't be made from the stock: ", "Bu malzemeden yapılamaz: ") +
                                         string.Join(" ", plan.Problems)), "job-card__warning");
                return;
            }

            // Money: what the job pays, what it costs now, what the workshop has.
            VisualElement money = Section(offerCard, Loc.T("MONEY", "PARA"), out _, out _);
            Money cost = jobBoard.MaterialCostAt(index);
            ValueRow(money, Loc.T("Pay (with all quality goals)", "Ücret (tüm kalite hedefleriyle)"), job.Pay.ToString(), "value-row__value--plus");
            if (job.Deposit > Money.Zero)
            {
                ValueRow(money, Loc.T("Deposit when accepting", "Kabulde avans"), job.Deposit.ToString(), "value-row__value--plus");
            }

            ValueRow(money, Loc.T("Material to buy", "Alınacak malzeme"), cost > Money.Zero ? (-cost).ToString() : Loc.T("none", "yok"),
                cost > Money.Zero ? "value-row__value--minus" : null);
            ValueRow(money, Loc.T("Your money", "Paran"), jobBoard.Balance.ToString(), null, total: true);

            // Material: what it takes, what the workshop has, what is bought.
            string spare = job.SpareBoards > 0 ? Loc.T($"+ {job.SpareBoards} spare", $"+ {job.SpareBoards} yedek") : string.Empty;
            VisualElement material = Section(offerCard, Loc.T("MATERIAL", "MALZEME"), out _, out Label spareLabel);
            spareLabel.text = spare;
            foreach (ShoppingLine line in jobBoard.Shopping(index).Lines)
            {
                bool enough = line.Available >= line.Needed;
                VisualElement row = Add(material, new VisualElement(), "cut-row");
                VisualElement dot = Add(row, new VisualElement(), "cut-row__dot");
                dot.AddToClassList(enough ? "cut-row__dot--done" : line.IsSold ? "cut-row__dot--started" : "cut-row__dot--problem");
                Add(row, new Label($"{line.Needed} ×"), "cut-row__qty");
                Add(row, new Label(line.Description), "cut-row__name");
                Label status = Add(row, new Label(enough
                    ? Loc.T($"have {line.Available}", $"var {line.Available}")
                    : line.IsSold
                        ? Loc.T($"buy {line.UnitsToBuy} · {line.Cost}", $"al {line.UnitsToBuy} · {line.Cost}")
                        : Loc.T("not sold", "satıcıda yok")), "cut-row__status");
                status.EnableInClassList("cut-row__status--done", enough);
                status.EnableInClassList("cut-row__status--problem", !enough && !line.IsSold);
            }

            // Cut plan per board: rip into strips first, then the parts, and what is left over.
            VisualElement cuts = Section(offerCard, Loc.T("CUT PLAN", "KESİM PLANI"), out _, out Label kerf);
            kerf.text = Loc.T($"kerf {plan.Kerf}", $"testere payı {plan.Kerf}");
            for (int i = 0; i < plan.Boards.Count && i < MaxRows; i++)
            {
                StockBoardPlan board = plan.Boards[i];
                var parts = new List<string>();
                foreach (MeasuredLength part in board.Parts)
                {
                    parts.Add(part.FormatMillimeters());
                }

                string rip = board.RipWidth.HasValue
                    ? Loc.T($"{board.Strips} × {board.RipWidth.Value} strip: ", $"{board.Strips} × {board.RipWidth.Value} şerit: ")
                    : string.Empty;
                VisualElement row = Add(cuts, new VisualElement(), "cut-row");
                Add(row, new Label($"{i + 1}"), "cut-row__qty");
                Add(row, new Label(rip + string.Join(" + ", parts) + " mm"), "cut-row__name");
                Add(row, new Label(Loc.T($"offcut {board.Offcut}", $"artık {board.Offcut}")), "cut-row__status");
            }

            if (plan.Boards.Count > MaxRows)
            {
                Add(cuts, new Label(Loc.T($"+ {plan.Boards.Count - MaxRows} more boards", $"+ {plan.Boards.Count - MaxRows} tahta daha")),
                    "job-card__line");
            }
        }

        // ---- Result card (D-062) ----

        private void ShowResult(JobResult result)
        {
            if (result == null)
            {
                resultCard.AddToClassList(HiddenClass);
                builtResult = null;
                return;
            }

            JobSettlement settlement = jobBoard.Settlement;
            string fasteners = FastenerNames(result);
            string key = Loc.Current + "|" + JobText.Result(result, fasteners, settlement);
            if (key != builtResult)
            {
                builtResult = key;
                BuildResult(result, settlement, fasteners);
            }

            resultCard.RemoveFromClassList(HiddenClass);
        }

        private void BuildResult(JobResult result, JobSettlement settlement, string fastenerNames)
        {
            resultCard.Clear();
            AssemblyQualityReport report = result.Report;
            var blueprint = result.Job.Blueprint;

            VisualElement header = Add(resultCard, new VisualElement(), "job-card__header");
            Add(header, new Label(Loc.T("JOB COMPLETE", "İŞ TAMAMLANDI") + PlaceSuffix(jobBoard.ActiveJobPlace)), "job-card__eyebrow");
            Add(header, new Label(JobText.FormatTime(result.Seconds)), "job-card__time");
            Add(resultCard, new Label(result.Job.DisplayName), "job-card__title");

            // Quality goals: each met or not, with its measure.
            VisualElement goals = Section(resultCard, Loc.T("QUALITY GOALS", "KALİTE HEDEFLERİ"), out _, out Label goalCount);
            goalCount.text = $"{result.GoalsMet}/{result.Goals.Count}";
            foreach (QualityGoal goal in result.Goals)
            {
                VisualElement row = Add(goals, new VisualElement(), "cut-row");
                Add(row, new VisualElement(), "cut-row__dot").AddToClassList(goal.Met ? "cut-row__dot--done" : "cut-row__dot--problem");
                Add(row, new Label(goal.Name), "cut-row__name");
                Label detail = Add(row, new Label(goal.Detail), "cut-row__status");
                detail.EnableInClassList("cut-row__status--done", goal.Met);
                detail.EnableInClassList("cut-row__status--problem", !goal.Met);
            }

            // Parts: only those off size or missing (a terrace has dozens), then the fasteners in one line.
            VisualElement parts = Section(resultCard, Loc.T("PARTS", "PARÇALAR"), out _, out Label accurate);
            accurate.text = Loc.T($"{report.AccurateParts}/{report.PartsRequired} accurate ({blueprint.AccuracyTolerance})",
                $"{report.AccurateParts}/{report.PartsRequired} ölçüsünde ({blueprint.AccuracyTolerance})");
            int problems = 0;
            foreach (PartQuality part in report.Parts)
            {
                if (part.IsAccurate)
                {
                    continue;
                }

                if (++problems > MaxRows)
                {
                    continue;
                }

                VisualElement row = Add(parts, new VisualElement(), "cut-row");
                Add(row, new VisualElement(), "cut-row__dot").AddToClassList(part.IsInstalled ? "cut-row__dot--started" : "cut-row__dot--problem");
                Add(row, new Label(part.Slot.DisplayName), "cut-row__name");
                string state = !part.IsInstalled
                    ? Loc.T("missing", "eksik")
                    : part.Fit.HasValue
                        ? $"{part.Piece.Dimensions.Length}  ({Signed(part.Fit.Value.Length.Deviation)})"
                        : part.Piece.Dimensions.Length.ToString();
                Add(row, new Label(state), "cut-row__status").AddToClassList("cut-row__status--problem");
            }

            if (problems == 0)
            {
                Add(parts, new Label(Loc.T("All parts within the tolerance.", "Tüm parçalar tolerans içinde.")), "job-card__line");
            }
            else if (problems > MaxRows)
            {
                Add(parts, new Label(Loc.T($"+ {problems - MaxRows} more", $"+ {problems - MaxRows} tane daha")), "job-card__line");
            }

            Add(parts, new Label(FastenerLine(report, fastenerNames)), "job-card__line");

            // Material: how much of the delivered wood is in the structure.
            MaterialUsage material = result.Material;
            VisualElement wood = Section(resultCard, Loc.T("MATERIAL", "MALZEME"), out _, out Label used);
            used.text = Loc.T($"{JobText.Percent(material.Utilization)} used", $"{JobText.Percent(material.Utilization)} kullanıldı");
            string extra = material.BoardsOrderedExtra > 0
                ? Loc.T($" ({material.BoardsOrderedExtra} ordered extra)", $" ({material.BoardsOrderedExtra} ek sipariş)")
                : string.Empty;
            Add(wood, new Label(Loc.T($"{material.BoardsDelivered} board(s) delivered{extra}", $"{material.BoardsDelivered} tahta teslim edildi{extra}")),
                "job-card__line");
            Add(wood, new Label(Loc.T(
                $"structure {JobText.CubicCentimetres(material.InstalledCubicMeters)} cm³ · sawdust {JobText.CubicCentimetres(material.SawdustCubicMeters)} cm³ · offcuts {JobText.CubicCentimetres(material.OffcutCubicMeters)} cm³",
                $"yapı {JobText.CubicCentimetres(material.InstalledCubicMeters)} cm³ · talaş {JobText.CubicCentimetres(material.SawdustCubicMeters)} cm³ · artık {JobText.CubicCentimetres(material.OffcutCubicMeters)} cm³")),
                "job-card__line");

            // Pay: delivery and goals, minus the material, the profit.
            if (settlement != null)
            {
                VisualElement pay = Section(resultCard, Loc.T("PAY", "ÜCRET"), out _, out Label ofPay);
                ofPay.text = Loc.T($"of {settlement.Pay}", $"{settlement.Pay} üzerinden");
                ValueRow(pay, Loc.T("Delivery", "Teslim"), settlement.BasePay.ToString(), "value-row__value--plus");
                ValueRow(pay, Loc.T($"Quality goals {result.GoalsMet}/{result.Goals.Count}", $"Kalite hedefleri {result.GoalsMet}/{result.Goals.Count}"),
                    settlement.Bonus.ToString(), "value-row__value--plus");
                ValueRow(pay, Loc.T("Timber used", "Kullanılan ahşap"), (-settlement.TimberCost).ToString(), "value-row__value--minus");
                ValueRow(pay, Loc.T("Fasteners used", "Kullanılan vida"), (-settlement.FastenerCost).ToString(), "value-row__value--minus");
                ValueRow(pay, Loc.T("Profit", "Kâr"), settlement.Profit.ToString(), null, total: true);
                if (settlement.Unpriced.Count > 0)
                {
                    Add(pay, new Label(Loc.T("No price for: ", "Fiyatı yok: ") + string.Join(", ", settlement.Unpriced)), "job-card__small");
                }

                Add(pay, new Label(Loc.T("Offcuts stay in the workshop.", "Artıklar atölyede kalır.")), "job-card__small");
            }

            Add(resultCard, new Label(AssemblyReportText.SafetyNote), "job-card__small");
        }

        /// <summary>"Screws 24/24 (…): 22 flush, 2 standing out, 1 split", glue still drying.</summary>
        private static string FastenerLine(AssemblyQualityReport report, string fastenerNames)
        {
            string names = string.IsNullOrEmpty(fastenerNames) ? string.Empty : $" ({fastenerNames})";
            string line = Loc.T("Fasteners", "Vidalar") + $" {report.FastenersInstalled}/{report.FastenersRequired}{names}";
            if (report.FastenersInstalled > 0)
            {
                line += Loc.T($": {report.FlushFasteners} flush", $": {report.FlushFasteners} hizada");
                if (report.ProudFasteners > 0)
                {
                    line += Loc.T($", {report.ProudFasteners} standing out", $", {report.ProudFasteners} dışarıda");
                }

                if (report.OverdrivenFasteners > 0)
                {
                    line += Loc.T($", {report.OverdrivenFasteners} too deep", $", {report.OverdrivenFasteners} fazla gömülü");
                }

                if (report.Splits > 0)
                {
                    line += Loc.T($", {report.Splits} split", $", {report.Splits} çatlak");
                }
            }

            if (report.DryingJoints > 0)
            {
                line += Loc.T($"; glue drying at {report.DryingJoints}", $"; {report.DryingJoints} yerde tutkal kuruyor");
            }

            return line;
        }

        private static string Signed(MeasuredLength deviation) => deviation > MeasuredLength.Zero ? "+" + deviation : deviation.ToString();

        // ---- Building blocks ----

        /// <summary>A card section with a separator line and a heading row (title left, two values right).</summary>
        private static VisualElement Section(VisualElement parent, string title, out Label middle, out Label right)
        {
            VisualElement section = Add(parent, new VisualElement(), "job-card__section");
            VisualElement heading = Add(section, new VisualElement(), "job-card__heading");
            Add(heading, new Label(title), "job-card__heading-title");
            middle = Add(heading, new Label(), "job-card__heading-value");
            Add(heading, new VisualElement(), "job-card__spacer");
            right = Add(heading, new Label(), "job-card__heading-value");
            return section;
        }

        /// <summary>A row with a name left and an amount right (optionally coloured, or a bold total).</summary>
        private static void ValueRow(VisualElement parent, string name, string value, string valueClass, bool total = false)
        {
            VisualElement row = Add(parent, new VisualElement(), "value-row");
            row.EnableInClassList("value-row--total", total);
            Add(row, new Label(name), "value-row__name");
            Label amount = Add(row, new Label(value), "value-row__value");
            if (valueClass != null)
            {
                amount.AddToClassList(valueClass);
            }
        }

        private static T Add<T>(VisualElement parent, T child, string className) where T : VisualElement
        {
            child.AddToClassList(className);
            child.pickingMode = PickingMode.Ignore;
            parent.Add(child);
            return child;
        }

        /// <summary>"  ·  ISLAND HOUSE" for a job at a customer's place (D-056), else empty. Turkish i → İ.</summary>
        private static string PlaceSuffix(World.LocationDefinition place)
        {
            if (place == null)
            {
                return string.Empty;
            }

            string name = place.DisplayName;
            return "  ·  " + (Loc.IsTurkish ? name.Replace('i', 'İ') : name).ToUpperInvariant();
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

        /// <summary>First letter upper case; Turkish i → İ (no culture lookup: invariant globalization in builds).</summary>
        private static string Capitalized(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            char first = Loc.IsTurkish && text[0] == 'i' ? 'İ' : char.ToUpperInvariant(text[0]);
            return first + text.Substring(1);
        }

        private string FastenerNames(JobResult result)
        {
            var names = new List<string>();
            foreach (var connection in result.Job.Blueprint.Connections)
            {
                string fastenerName = jobBoard.FastenerName(connection.FastenerId);
                if (!names.Contains(fastenerName))
                {
                    names.Add(fastenerName);
                }
            }

            return string.Join(", ", names);
        }
    }
}
