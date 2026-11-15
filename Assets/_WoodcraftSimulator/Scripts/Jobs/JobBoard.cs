using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;
using WoodcraftSimulator.Persistence;
using WoodcraftSimulator.Tools;

namespace WoodcraftSimulator.Jobs
{
    /// <summary>
    /// The job board on the workshop wall (Phase 6, D-039): one sheet per job (<see cref="JobSheet"/>). Accepting a job
    /// sets the assembly station's blueprint, delivers the calculated stock next to the timber rack and puts the job's
    /// cut list on the bench note. When the structure is complete the result is shown; delivering it clears the
    /// station. One job at a time; more stock can be ordered while it runs (it shows in the result).
    /// </summary>
    public sealed class JobBoard : MonoBehaviour
    {
        [SerializeField] private JobDefinition[] jobs = Array.Empty<JobDefinition>();
        [SerializeField] private AssemblyStation station;
        [SerializeField] private TimberBoardFactory boardFactory;
        [SerializeField] private CutList cutList;
        [SerializeField] private WorkshopWaste waste;

        [Tooltip("The saw the material plan assumes (kerf per cut).")]
        [SerializeField] private SawDefinition saw;

        [Tooltip("Stock is laid on the floor here: lengthwise along the forward axis, side by side along the right axis.")]
        [SerializeField] private Transform deliveryPoint;

        [Tooltip("Fastener types (display names in the cut list).")]
        [SerializeField] private FastenerDefinition[] fasteners = Array.Empty<FastenerDefinition>();

        private const int BoardsPerRow = 6;
        private const float BoardGap = 0.03f;

        private readonly List<string> completedJobIds = new List<string>();
        private Job[] domainJobs;
        private MaterialPlan[] plans;
        private int activeIndex = -1;
        private int boardsDelivered;
        private int boardsOrderedExtra;
        private double sawdustAtStart;
        private JobResult cachedResult;

        public IReadOnlyList<JobDefinition> Jobs => jobs;

        public bool HasActiveJob => activeIndex >= 0;

        public Job ActiveJob => HasActiveJob ? domainJobs[activeIndex] : null;

        public MaterialPlan ActivePlan => HasActiveJob ? plans[activeIndex] : null;

        /// <summary>Playing time since the job was accepted (stops when it is complete).</summary>
        public double ElapsedSeconds { get; private set; }

        public int BoardsDelivered => boardsDelivered;

        public IReadOnlyList<string> CompletedJobIds => completedJobIds;

        /// <summary>The structure of the active job is complete (waiting to be delivered).</summary>
        public bool IsComplete => HasActiveJob && station.Build.IsComplete;

        /// <summary>Raised after a job was accepted, completed, delivered or more stock was ordered.</summary>
        public event Action Changed;

        private void Awake()
        {
            if (station == null || boardFactory == null || saw == null || deliveryPoint == null)
            {
                Debug.LogError($"{nameof(JobBoard)}: Station, Board Factory, Saw and Delivery Point must be assigned.", this);
                enabled = false;
                return;
            }

            Length kerf = saw.ToDomain().Kerf;
            domainJobs = new Job[jobs.Length];
            plans = new MaterialPlan[jobs.Length];
            for (int i = 0; i < jobs.Length; i++)
            {
                domainJobs[i] = jobs[i].ToDomain();
                plans[i] = domainJobs[i].Plan(kerf);
                if (!plans[i].IsFeasible)
                {
                    Debug.LogWarning($"[{nameof(JobBoard)}] Job '{jobs[i].Id}' can't be made from its stock: " +
                                     string.Join(" ", plans[i].Problems), this);
                }
            }
        }

        private void OnEnable()
        {
            if (station != null)
            {
                station.Changed += OnStationChanged;
                station.Completed += OnStationCompleted;
            }
        }

        private void OnDisable()
        {
            if (station != null)
            {
                station.Changed -= OnStationChanged;
                station.Completed -= OnStationCompleted;
            }
        }

        private void Update()
        {
            // Time.deltaTime is 0 while paused, so the pause menu doesn't count.
            if (HasActiveJob && !IsComplete)
            {
                ElapsedSeconds += Time.deltaTime;
            }
        }

        private void OnStationChanged() => cachedResult = null;

        private void OnStationCompleted()
        {
            if (HasActiveJob)
            {
                cachedResult = null;
                Changed?.Invoke();
            }
        }

        /// <summary>The result of the active job once its structure is complete; null before.</summary>
        public JobResult Result
        {
            get
            {
                if (!IsComplete)
                {
                    return null;
                }

                if (cachedResult == null)
                {
                    AssemblyQualityReport report = station.Build.CreateReport();
                    double installed = 0.0;
                    foreach (PartQuality part in report.Parts)
                    {
                        installed += part.Piece != null ? part.Piece.Dimensions.VolumeCubicMeters : 0.0;
                    }

                    double delivered = boardsDelivered * ActiveJob.Stock.Dimensions.VolumeCubicMeters;
                    double sawdust = waste != null ? Math.Max(0.0, waste.Log.TotalVolumeCubicMeters - sawdustAtStart) : 0.0;
                    cachedResult = JobResult.Create(ActiveJob, report,
                        new MaterialUsage(boardsDelivered, boardsOrderedExtra, delivered, installed, sawdust), ElapsedSeconds);
                }

                return cachedResult;
            }
        }

        public string FastenerName(string fastenerId)
        {
            foreach (FastenerDefinition definition in fasteners)
            {
                if (definition != null && definition.Id == fastenerId)
                {
                    return definition.DisplayName;
                }
            }

            return fastenerId;
        }

        /// <summary>The job sheet of the active job: description, material, cut list with progress.</summary>
        public string SheetText()
        {
            if (!HasActiveJob)
            {
                return null;
            }

            // Loose pieces lying in the workshop count as "ready" for the cut list (installed ones are counted by slot).
            var loose = new List<TimberPiece>();
            foreach (TimberBoard board in FindObjectsByType<TimberBoard>(FindObjectsSortMode.None))
            {
                if (board.Piece != null && (!board.TryGetComponent(out Carryable carryable) || carryable.InstalledIn == null))
                {
                    loose.Add(board.Piece);
                }
            }

            IReadOnlyList<CutListLineProgress> progress = CutListProgress.Evaluate(ActivePlan, station.Build, loose);
            return JobText.Sheet(ActiveJob, ActivePlan, progress, FastenerName, boardsDelivered, ElapsedSeconds);
        }

        /// <summary>What a job needs, before accepting it.</summary>
        public string OfferText(int index) =>
            index >= 0 && index < jobs.Length ? JobText.Offer(domainJobs[index], plans[index], FastenerName) : null;

        public string PromptFor(int index)
        {
            if (index < 0 || index >= jobs.Length)
            {
                return null;
            }

            string jobName = jobs[index].DisplayName;
            if (!HasActiveJob)
            {
                if (!plans[index].IsFeasible)
                {
                    return $"{jobName}: can't be made from the stock (see Console)";
                }

                return station.IsEmpty
                    ? $"Accept job: {jobName}   ({BoardsToDeliver(index)} board(s) delivered)"
                    : $"Accept job: {jobName}   (clear the {station.DisplayName} first)";
            }

            if (index != activeIndex)
            {
                return $"Job in progress: {jobs[activeIndex].DisplayName}";
            }

            return IsComplete
                ? $"Deliver the {jobName} (finish the job)"
                : $"Order 1 more board   ({ActiveJob.Stock})";
        }

        public void Interact(int index)
        {
            if (index < 0 || index >= jobs.Length)
            {
                return;
            }

            if (!HasActiveJob)
            {
                Accept(index);
            }
            else if (index == activeIndex)
            {
                if (IsComplete)
                {
                    Deliver();
                }
                else
                {
                    DeliverStock(1);
                    boardsOrderedExtra++;
                    cachedResult = null;
                    Changed?.Invoke();
                }
            }
        }

        private int BoardsToDeliver(int index) => plans[index].BoardsNeeded + domainJobs[index].SpareBoards;

        private void Accept(int index)
        {
            if (!plans[index].IsFeasible || !station.IsEmpty || !station.TrySetBlueprint(jobs[index].Blueprint))
            {
                return;
            }

            activeIndex = index;
            ElapsedSeconds = 0.0;
            boardsDelivered = 0;
            boardsOrderedExtra = 0;
            sawdustAtStart = waste != null ? waste.Log.TotalVolumeCubicMeters : 0.0;
            cachedResult = null;
            DeliverStock(BoardsToDeliver(index));
            ShowCutList();
            Changed?.Invoke();
        }

        /// <summary>The finished structure goes to the customer: the station is cleared, the job is done.</summary>
        private void Deliver()
        {
            completedJobIds.Add(jobs[activeIndex].Id);
            station.ClearAll();
            activeIndex = -1;
            cachedResult = null;
            if (cutList != null)
            {
                cutList.ClearJobLines();
            }

            Changed?.Invoke();
        }

        private void ShowCutList()
        {
            if (cutList == null)
            {
                return;
            }

            var lengths = new List<Length>();
            var labels = new List<string>();
            foreach (CutListLine line in ActivePlan.Lines)
            {
                lengths.Add(line.Length);
                var names = new List<string>();
                foreach (PartSlot slot in line.Slots)
                {
                    names.Add(slot.DisplayName);
                }

                labels.Add($"{line.Quantity} x {string.Join(", ", names)}");
            }

            cutList.SetJobLines(lengths, labels);
        }

        /// <summary>Lays stock boards flat on the floor at the delivery point, next to any delivered before.</summary>
        private void DeliverStock(int count)
        {
            JobDefinition definition = jobs[activeIndex];
            BoardDimensions size = definition.StockDimensions;
            var species = definition.StockSpecies.ToDomain();
            float width = (float)size.Width.Meters;
            float thickness = (float)size.Thickness.Meters;
            for (int i = 0; i < count; i++)
            {
                int slot = boardsDelivered + i;
                int column = slot % BoardsPerRow;
                int layer = slot / BoardsPerRow;
                Vector3 position = deliveryPoint.position +
                                   deliveryPoint.right * (column * (width + BoardGap)) +
                                   deliveryPoint.up * (thickness * (layer + 0.5f) + 0.002f * (layer + 1));
                var piece = new TimberPiece(species, size);
                TimberBoard board = boardFactory.Create(Guid.NewGuid().ToString("N"), definition.StockSpecies, piece,
                    new BoardMarks(size.Length), position, deliveryPoint.rotation);
                board.GetComponent<Carryable>().RequestSupportCheck();
            }

            boardsDelivered += count;
        }

        /// <summary>Save state of the job system.</summary>
        public JobData Capture() => new JobData
        {
            activeJobId = HasActiveJob ? jobs[activeIndex].Id : string.Empty,
            elapsedSeconds = ElapsedSeconds,
            boardsDelivered = boardsDelivered,
            boardsOrderedExtra = boardsOrderedExtra,
            sawdustAtStartCubicMeters = sawdustAtStart,
            completedJobIds = completedJobIds.ToArray()
        };

        /// <summary>
        /// Loading a save (before the assemblies are restored): the active job, its blueprint on the station and its
        /// cut list. The delivered boards come back with the saved boards, so nothing is delivered again.
        /// </summary>
        public void Restore(JobData data)
        {
            completedJobIds.Clear();
            completedJobIds.AddRange(data.completedJobIds ?? Array.Empty<string>());
            activeIndex = -1;
            cachedResult = null;
            if (!string.IsNullOrEmpty(data.activeJobId))
            {
                activeIndex = Array.FindIndex(jobs, job => job != null && job.Id == data.activeJobId);
                if (activeIndex < 0)
                {
                    Debug.LogWarning($"[{nameof(JobBoard)}] Saved job '{data.activeJobId}' is unknown; no job is active.", this);
                }
            }

            ElapsedSeconds = data.elapsedSeconds;
            boardsDelivered = data.boardsDelivered;
            boardsOrderedExtra = data.boardsOrderedExtra;
            sawdustAtStart = data.sawdustAtStartCubicMeters;
            if (HasActiveJob)
            {
                station.TrySetBlueprint(jobs[activeIndex].Blueprint);
                ShowCutList();
            }

            Changed?.Invoke();
        }
    }
}
