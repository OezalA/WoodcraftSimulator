using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;
using WoodcraftSimulator.Persistence;
using WoodcraftSimulator.Tools;

namespace WoodcraftSimulator.Jobs
{
    /// <summary>
    /// The job board on the workshop wall (Phase 6, D-039; economy D-040): one sheet per job (<see cref="JobSheet"/>).
    /// Accepting a job buys what the workshop lacks (stock boards beyond those lying around, fastener boxes), sets the
    /// assembly station's blueprint and puts the job's cut list on the bench note. When the structure is complete the
    /// result with pay, cost and profit is shown; delivering it pays the job and clears the station. One job at a
    /// time; more stock can be bought while it runs (it shows in the result).
    /// </summary>
    public sealed class JobBoard : MonoBehaviour
    {
        [SerializeField] private JobDefinition[] jobs = Array.Empty<JobDefinition>();
        [SerializeField] private AssemblyStation station;

        [Tooltip("Further assembly places (e.g. the terrace site); a job is built where its blueprint is known.")]
        [SerializeField] private AssemblyStation[] otherStations = Array.Empty<AssemblyStation>();

        [SerializeField] private CutList cutList;
        [SerializeField] private WorkshopWaste waste;
        [SerializeField] private WorkshopEconomy economy;

        [Tooltip("The saw the material plan assumes (kerf per cut).")]
        [SerializeField] private SawDefinition saw;

        [Tooltip("Fastener types (display names in the cut list).")]
        [SerializeField] private FastenerDefinition[] fasteners = Array.Empty<FastenerDefinition>();

        private readonly List<string> completedJobIds = new List<string>();
        private Job[] domainJobs;
        private MaterialPlan[] plans;
        private int activeIndex = -1;
        private int boardsDelivered;
        private int boardsOrderedExtra;
        private double sawdustAtStart;
        private JobResult cachedResult;
        private JobSettlement cachedSettlement;
        private Money depositPaid;
        private string lastMessage;
        private float lastMessageUntil;

        public IReadOnlyList<JobDefinition> Jobs => jobs;

        public bool HasActiveJob => activeIndex >= 0;

        public Job ActiveJob => HasActiveJob ? domainJobs[activeIndex] : null;

        public MaterialPlan ActivePlan => HasActiveJob ? plans[activeIndex] : null;

        /// <summary>Playing time since the job was accepted (stops when it is complete).</summary>
        public double ElapsedSeconds { get; private set; }

        public int BoardsDelivered => boardsDelivered;

        public IReadOnlyList<string> CompletedJobIds => completedJobIds;

        /// <summary>The structure of the active job is complete (waiting to be delivered).</summary>
        public bool IsComplete => HasActiveJob && Active.Build.IsComplete;

        /// <summary>Raised after a job was accepted, completed, delivered or more stock was bought.</summary>
        public event Action Changed;

        private void Awake()
        {
            if (station == null || saw == null || economy == null)
            {
                Debug.LogError($"{nameof(JobBoard)}: Station, Saw and Economy must be assigned.", this);
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
            foreach (AssemblyStation each in AllStations())
            {
                each.Changed += OnStationChanged;
                each.Completed += OnStationCompleted;
            }

            TimberBoard.AnySurfaceChanged += OnStationChanged;
        }

        private void OnDisable()
        {
            foreach (AssemblyStation each in AllStations())
            {
                each.Changed -= OnStationChanged;
                each.Completed -= OnStationCompleted;
            }

            TimberBoard.AnySurfaceChanged -= OnStationChanged;
        }

        /// <summary>The workshop's assembly table and any other station (the terrace site outside, D-046).</summary>
        private IEnumerable<AssemblyStation> AllStations()
        {
            if (station != null)
            {
                yield return station;
            }

            foreach (AssemblyStation other in otherStations)
            {
                if (other != null)
                {
                    yield return other;
                }
            }
        }

        /// <summary>Where a job is built: the station that knows its blueprint.</summary>
        private AssemblyStation StationFor(int index)
        {
            foreach (AssemblyStation each in AllStations())
            {
                if (each.FindBlueprint(jobs[index].Blueprint.Id) != null)
                {
                    return each;
                }
            }

            return station;
        }

        /// <summary>The station of the active job (the workshop table when none is active).</summary>
        private AssemblyStation Active => HasActiveJob ? StationFor(activeIndex) : station;

        private void Update()
        {
            // Time.deltaTime is 0 while paused, so the pause menu doesn't count.
            if (HasActiveJob && !IsComplete)
            {
                ElapsedSeconds += Time.deltaTime;
            }
        }

        private void OnStationChanged()
        {
            cachedResult = null;
            cachedSettlement = null;
        }

        private void OnStationCompleted()
        {
            if (HasActiveJob)
            {
                OnStationChanged();
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
                    AssemblyQualityReport report = Active.Build.CreateReport();
                    double installed = 0.0;
                    foreach (PartQuality part in report.Parts)
                    {
                        installed += part.Piece != null ? part.Piece.Dimensions.VolumeCubicMeters : 0.0;
                    }

                    // The planned boards of every stock, plus extra boards of the main stock.
                    double delivered = boardsOrderedExtra * ActiveJob.Stock.Dimensions.VolumeCubicMeters;
                    foreach (StockBoardPlan planned in ActivePlan.Boards)
                    {
                        delivered += planned.Stock.Dimensions.VolumeCubicMeters;
                    }

                    double sawdust = waste != null ? Math.Max(0.0, waste.Log.TotalVolumeCubicMeters - sawdustAtStart) : 0.0;

                    // Outdoor jobs: stages the customer inspected, and those approved at the first try (D-053).
                    int inspected = 0;
                    int firstTime = 0;
                    if (Active.TryGetComponent(out Workshop.SitePreparation site))
                    {
                        inspected = site.InspectedStageCount(Active.Build);
                        firstTime = site.Inspections.PassedFirstTime;
                    }

                    cachedResult = JobResult.Create(ActiveJob, report,
                        new MaterialUsage(boardsDelivered, boardsOrderedExtra, delivered, installed, sawdust), ElapsedSeconds,
                        FinishedParts(), inspected, firstTime);
                }

                return cachedResult;
            }
        }

        /// <summary>Installed parts that are sanded and oiled (D-045).</summary>
        private int FinishedParts()
        {
            int finished = 0;
            for (int i = 0; i < Active.Build.Blueprint.Slots.Count; i++)
            {
                Handling.Carryable part = Active.InstalledAt(i);
                if (part != null && part.TryGetComponent(out TimberBoard board) && board.Surface == BoardSurface.Oiled)
                {
                    finished++;
                }
            }

            return finished;
        }

        /// <summary>Pay, material cost and profit of the completed job; null before.</summary>
        public JobSettlement Settlement
        {
            get
            {
                JobResult result = Result;
                if (result == null)
                {
                    return null;
                }

                return cachedSettlement ??= JobSettlement.Calculate(result, economy.Catalog);
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

        /// <summary>Progress of each cut list line of the active job (empty without one).</summary>
        public IReadOnlyList<CutListLineProgress> ActiveProgress() =>
            // Loose pieces lying in the workshop count as "ready" for the cut list (installed ones are counted by slot).
            HasActiveJob
                ? CutListProgress.Evaluate(ActivePlan, Active.Build, WorkshopEconomy.LooseTimber())
                : Array.Empty<CutListLineProgress>();

        /// <summary>
        /// Whether a loose piece fits a part of the active job that is not installed yet (D-052: the apprentice leaves
        /// such pieces where they are).
        /// </summary>
        public bool NeedsPiece(TimberPiece piece)
        {
            if (!HasActiveJob || piece == null)
            {
                return false;
            }

            foreach (CutListLine line in ActivePlan.Lines)
            {
                foreach (PartSlot slot in line.Slots)
                {
                    if (Active.Build.GetPiece(slot.Id) == null && Active.Build.CheckFit(slot, piece).Fits)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The construction stage of an outdoor job (D-046); false for a bench job.</summary>
        public bool TryGetStage(out Workshop.SitePreparation.StageState stage)
        {
            stage = default;
            if (!HasActiveJob || !Active.TryGetComponent(out Workshop.SitePreparation site))
            {
                return false;
            }

            stage = site.CurrentStage(Active.Build);
            return true;
        }

        /// <summary>What a job needs, before accepting it: material plan and what has to be bought.</summary>
        public string OfferText(int index) =>
            index >= 0 && index < jobs.Length
                ? JobText.Offer(domainJobs[index], plans[index], FastenerName, Shopping(index))
                : null;

        /// <summary>The job's bill of materials against what the workshop has (stock boards lying around, fasteners).</summary>
        public ShoppingList Shopping(int index)
        {
            List<TimberPiece> loose = WorkshopEconomy.LooseTimber();
            return ShoppingList.For(plans[index], economy.Catalog, stock => WorkshopInventory.CountStockBoards(loose, stock),
                economy.Fasteners, FastenerName);
        }

        public string PromptFor(int index)
        {
            if (index < 0 || index >= jobs.Length)
            {
                return null;
            }

            string prompt = BasePrompt(index);
            if (lastMessage != null && Time.unscaledTime < lastMessageUntil)
            {
                prompt += "\n" + lastMessage;
            }

            return prompt;
        }

        private string BasePrompt(int index)
        {
            string jobName = jobs[index].DisplayName;
            if (!HasActiveJob)
            {
                if (!plans[index].IsFeasible)
                {
                    return Loc.T($"{jobName}: can't be made from the stock (see Console)", $"{jobName}: bu malzemeden yapılamaz (Console'a bak)");
                }

                if (!StationFor(index).IsEmpty)
                {
                    return Loc.T($"Accept job: {jobName}   (clear the {StationFor(index).DisplayName} first)",
                        $"İşi kabul et: {jobName}   (önce {StationFor(index).DisplayName} boşaltılmalı)");
                }

                Money pay = domainJobs[index].Pay;
                Money cost = Shopping(index).TotalCost;
                Money deposit = domainJobs[index].Deposit;
                string depositText = deposit > Money.Zero ? Loc.T($", deposit {deposit} now", $", avans {deposit} hemen") : string.Empty;
                return Loc.T($"Accept job: {jobName}   (pay {pay}{depositText}; material to buy {cost}, money {economy.Ledger.Balance})",
                    $"İşi kabul et: {jobName}   (ücret {pay}{depositText}; alınacak malzeme {cost}, para {economy.Ledger.Balance})");
            }

            if (index != activeIndex)
            {
                return Loc.T($"Job in progress: {jobs[activeIndex].DisplayName}", $"Devam eden iş: {jobs[activeIndex].DisplayName}");
            }

            if (IsComplete)
            {
                JobSettlement settlement = Settlement;
                return Loc.T($"Deliver the {jobName}: get paid {settlement.Earned}", $"{jobName} teslim et: {settlement.Earned} ödenir");
            }

            CatalogItem board = economy.Catalog.FindBoard(ActiveJob.Stock);
            return board != null
                ? Loc.T($"Buy 1 more board   ({ActiveJob.Stock}, {board.UnitPrice})",
                    $"1 tahta daha satın al   ({ActiveJob.Stock}, {board.UnitPrice})")
                : Loc.T("The merchant doesn't sell this stock", "Satıcıda bu malzeme yok");
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
                    BuyExtraBoard();
                }
            }
        }

        private void Accept(int index)
        {
            if (!plans[index].IsFeasible || !StationFor(index).IsEmpty)
            {
                return;
            }

            // Buy what the workshop lacks first: a job is only taken on when its material can be had.
            ShoppingList shopping = Shopping(index);
            foreach (ShoppingLine line in shopping.Lines)
            {
                if (!line.IsSold && line.Available < line.Needed)
                {
                    ShowMessage(Loc.T($"Not sold by the merchant: {line.Description}", $"Satıcıda yok: {line.Description}"));
                    return;
                }
            }

            // A deposit from the customer (D-046) helps to pay for the material.
            Money deposit = domainJobs[index].Deposit;
            if (economy.Ledger.Balance + deposit < shopping.TotalCost)
            {
                ShowMessage(Loc.T($"Not enough money for the material ({shopping.TotalCost})",
                    $"Malzeme için para yetmiyor ({shopping.TotalCost})"));
                return;
            }

            if (!StationFor(index).TrySetBlueprint(jobs[index].Blueprint))
            {
                return;
            }

            if (deposit > Money.Zero)
            {
                economy.Ledger.Earn(deposit, Loc.T($"Deposit: {jobs[index].DisplayName}", $"Avans: {jobs[index].DisplayName}"));
            }

            depositPaid = deposit;
            foreach (ShoppingLine line in shopping.Lines)
            {
                if (line.UnitsToBuy > 0)
                {
                    economy.Buy(line.Item, line.UnitsToBuy);
                }
            }

            activeIndex = index;
            ElapsedSeconds = 0.0;
            boardsDelivered = plans[index].BoardsNeeded; // the boards set aside for the job, bought or from stock
            boardsOrderedExtra = 0;
            sawdustAtStart = waste != null ? waste.Log.TotalVolumeCubicMeters : 0.0;
            OnStationChanged();
            ShowCutList();
            ShowMessage(shopping.TotalCost > Money.Zero
                ? (StationFor(index) == station
                    ? Loc.T($"Material bought ({shopping.TotalCost}): see the delivery area", $"Malzeme alındı ({shopping.TotalCost}): teslim alanına bak")
                    : Loc.T($"Material bought ({shopping.TotalCost}): delivered to the site outside", $"Malzeme alındı ({shopping.TotalCost}): dışarıdaki şantiyeye bırakıldı"))
                : Loc.T("The workshop has all the material", "Atölyede tüm malzeme var"));
            Changed?.Invoke();
        }

        private void BuyExtraBoard()
        {
            CatalogItem board = economy.Catalog.FindBoard(ActiveJob.Stock);
            if (board == null)
            {
                return;
            }

            PurchaseResult result = economy.Buy(board, 1);
            if (result != PurchaseResult.Bought)
            {
                ShowMessage(WorkshopEconomy.PurchaseText(result));
                return;
            }

            boardsDelivered++;
            boardsOrderedExtra++;
            OnStationChanged();
            ShowMessage(Loc.T("Bought: see the delivery area", "Satın alındı: teslim alanına bak"));
            Changed?.Invoke();
        }

        /// <summary>The finished structure goes to the customer: the job is paid, the station is cleared.</summary>
        private void Deliver()
        {
            JobSettlement settlement = Settlement;
            string jobName = jobs[activeIndex].DisplayName;
            // The deposit was paid on acceptance; the rest now (the deposit never exceeds the delivery share).
            Money rest = settlement.Earned - depositPaid;
            if (rest > Money.Zero)
            {
                economy.Ledger.Earn(rest, Loc.T($"Job: {jobName}", $"İş: {jobName}"));
            }

            depositPaid = Money.Zero;
            completedJobIds.Add(jobs[activeIndex].Id);
            Active.ClearAll();
            activeIndex = -1;
            OnStationChanged();
            if (cutList != null)
            {
                cutList.ClearJobLines();
            }

            ShowMessage(Loc.T($"Delivered: +{settlement.Earned} (profit {settlement.Profit})",
                $"Teslim edildi: +{settlement.Earned} (kâr {settlement.Profit})"));
            Changed?.Invoke();
        }

        private void ShowMessage(string message)
        {
            lastMessage = message;
            lastMessageUntil = Time.unscaledTime + 4f;
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

        /// <summary>Save state of the job system.</summary>
        public JobData Capture() => new JobData
        {
            activeJobId = HasActiveJob ? jobs[activeIndex].Id : string.Empty,
            elapsedSeconds = ElapsedSeconds,
            boardsDelivered = boardsDelivered,
            boardsOrderedExtra = boardsOrderedExtra,
            sawdustAtStartCubicMeters = sawdustAtStart,
            depositCents = depositPaid.Cents,
            completedJobIds = completedJobIds.ToArray()
        };

        /// <summary>
        /// Loading a save (before the assemblies are restored): the active job, its blueprint on the station and its
        /// cut list. The bought boards come back with the saved boards, so nothing is bought again.
        /// </summary>
        public void Restore(JobData data)
        {
            completedJobIds.Clear();
            completedJobIds.AddRange(data.completedJobIds ?? Array.Empty<string>());
            activeIndex = -1;
            OnStationChanged();
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
            depositPaid = Money.FromCents(data.depositCents);
            if (HasActiveJob)
            {
                StationFor(activeIndex).TrySetBlueprint(jobs[activeIndex].Blueprint);
                ShowCutList();
            }

            Changed?.Invoke();
        }
    }
}
