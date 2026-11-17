using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Workshop;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// HUD part for jobs (UI Toolkit, same document as <see cref="GameplayHud"/>). Top left: the job card of the active
    /// job (D-051: header with time, stage with progress bar, cut list as a table, material, tips), or the offer of
    /// the job sheet being looked at; on the right the result once the structure is complete. The card's elements are
    /// built once per job (and language) and only their texts and bars are updated, a few times per second (the cut
    /// list counts the pieces lying around). Styling: UI/Hud.uss, classes <c>job-card*</c> and <c>cut-row*</c>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class JobPanel : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        [SerializeField] private JobBoard jobBoard;
        [SerializeField] private PlayerInteractor interactor;

        [Tooltip("Seconds between refreshes of the job card.")]
        [SerializeField, Min(0.05f)] private float refreshSeconds = 0.5f;

        private Label sheetLabel;
        private Label resultLabel;
        private VisualElement card;
        private float nextRefresh;

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
            sheetLabel = root.Q<Label>("job-sheet");
            resultLabel = root.Q<Label>("job-result");
            card = root.Q<VisualElement>("job-card");
            if (sheetLabel == null || resultLabel == null || card == null || jobBoard == null || interactor == null)
            {
                Debug.LogError($"{nameof(JobPanel)}: needs 'job-sheet', 'job-card' and 'job-result' in the UXML, a Job Board " +
                               "and an Interactor.", this);
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
                Show(sheetLabel, null);
            }
            else
            {
                card.AddToClassList(HiddenClass);
                builtJob = null;
                string offer = interactor.CurrentTarget is JobSheet jobSheet && jobSheet.Board == jobBoard
                    ? jobBoard.OfferText(jobSheet.JobIndex)
                    : null;
                Show(sheetLabel, offer);
            }

            JobResult result = jobBoard.Result;
            Show(resultLabel, result != null ? JobText.Result(result, FastenerNames(result), jobBoard.Settlement) : null);
        }

        private void BuildCard(Job job, MaterialPlan plan)
        {
            builtJob = job;
            builtLanguage = Loc.Current;
            card.Clear();
            cutRows.Clear();

            // Header: what this is, the time; then the job's name and one line about it.
            VisualElement header = Add(card, new VisualElement(), "job-card__header");
            Add(header, new Label(Loc.T("ACTIVE JOB", "AKTİF İŞ")), "job-card__eyebrow");
            timeLabel = Add(header, new Label(), "job-card__time");
            Add(card, new Label(job.DisplayName), "job-card__title");
            if (job.Description.Length > 0)
            {
                Add(card, new Label(job.Description), "job-card__description");
            }

            // Outdoor jobs: the construction stage with a progress bar (D-046, D-049).
            stageSection = Section(Loc.T("STAGE", "AŞAMA"), out stageNumberLabel, out stageCountLabel);
            stageNameLabel = Add(stageSection, new Label(), "job-card__stage-name");
            VisualElement bar = Add(stageSection, new VisualElement(), "job-card__bar");
            stageFill = Add(bar, new VisualElement(), "job-card__bar-fill");
            stageNoteLabel = Add(stageSection, new Label(), "job-card__note");

            // Cut list as a table: status, quantity, size, parts, progress.
            VisualElement cutSection = Section(Loc.T("CUT LIST", "KESİM LİSTESİ"), out _, out cutSummaryLabel);
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
            VisualElement material = Section(Loc.T("MATERIAL", "MALZEME"), out _, out _);
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
                VisualElement tips = Section(Loc.T("TIPS", "İPUÇLARI"), out _, out _);
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

        /// <summary>A card section with a separator line and a heading row (title left, two values right).</summary>
        private VisualElement Section(string title, out Label middle, out Label right)
        {
            VisualElement section = Add(card, new VisualElement(), "job-card__section");
            VisualElement heading = Add(section, new VisualElement(), "job-card__heading");
            Add(heading, new Label(title), "job-card__heading-title");
            middle = Add(heading, new Label(), "job-card__heading-value");
            Add(heading, new VisualElement(), "job-card__spacer");
            right = Add(heading, new Label(), "job-card__heading-value");
            return section;
        }

        private static T Add<T>(VisualElement parent, T child, string className) where T : VisualElement
        {
            child.AddToClassList(className);
            child.pickingMode = PickingMode.Ignore;
            parent.Add(child);
            return child;
        }

        private static string SlotNames(CutListLine line)
        {
            var names = new List<string>();
            foreach (Construction.PartSlot slot in line.Slots)
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

        private static void Show(Label label, string text)
        {
            bool visible = !string.IsNullOrEmpty(text);
            if (visible && label.text != text)
            {
                label.text = text;
            }

            label.EnableInClassList(HiddenClass, !visible);
        }
    }
}
