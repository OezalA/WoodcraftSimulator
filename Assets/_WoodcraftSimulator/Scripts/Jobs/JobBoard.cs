using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Career;
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

        // The job service lives in the core scene (D-056); the assembly stations and the bench's cut list note are in
        // the content scenes and found at runtime (AssemblyStation.All, Services).
        private CutList cutList;
        [SerializeField] private WorkshopWaste waste;
        [SerializeField] private WorkshopEconomy economy;

        [Tooltip("The saw the material plan assumes (kerf per cut).")]
        [SerializeField] private SawDefinition saw;

        [Tooltip("Fastener types (display names in the cut list).")]
        [SerializeField] private FastenerDefinition[] fasteners = Array.Empty<FastenerDefinition>();

        [Tooltip("The player's career (D-054): which jobs are offered, pay factor, weekly costs.")]
        [SerializeField] private WorkshopCareer career;

        [Tooltip("Sheets on the board = offers per week (D-054).")]
        [SerializeField, Min(1)] private int slots = 5;

        private readonly List<string> completedJobIds = new List<string>();
        private readonly CareerProgress fallbackCareer = new CareerProgress();
        private readonly System.Random random = new System.Random();
        private Length kerf;
        private JobTemplateInfo[] templates;

        // Per sheet (slot): the offer, its template (index into jobs, -1 = empty), its job and material plan.
        private JobOffer[] offers;
        private int[] templateOf;
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

        /// <summary>Number of sheets (offers) on the board.</summary>
        public int Slots => slots;

        public CareerProgress CareerState => career != null ? career.Progress : fallbackCareer;

        public WorkshopCareer WorkshopCareer => career;

        /// <summary>The offer on a sheet, or null.</summary>
        public JobOffer OfferAt(int slot) => offers != null && slot >= 0 && slot < offers.Length ? offers[slot] : null;

        /// <summary>What a sheet shows from afar: the job's name, or "empty".</summary>
        public string SheetTitle(int slot) =>
            TemplateAt(slot) is JobDefinition template ? template.DisplayName : Loc.T("Empty sheet", "Boş kağıt");

        public bool HasActiveJob => activeIndex >= 0;

        public Job ActiveJob => HasActiveJob ? domainJobs[activeIndex] : null;

        public MaterialPlan ActivePlan => HasActiveJob ? plans[activeIndex] : null;

        /// <summary>Playing time since the job was accepted (stops when it is complete).</summary>
        public double ElapsedSeconds { get; private set; }

        public int BoardsDelivered => boardsDelivered;

        public IReadOnlyList<string> CompletedJobIds => completedJobIds;

        /// <summary>The structure of the active job is complete (waiting to be delivered).</summary>
        public bool IsComplete => Active != null && Active.Build.IsComplete;

        /// <summary>The active job's station is not in a loaded scene (the job is at a place the player isn't, D-056).</summary>
        public bool ActiveJobElsewhere => HasActiveJob && Active == null;

        /// <summary>The bench's cut list note, if the workshop is loaded.</summary>
        private CutList NoteOnBench => cutList != null ? cutList : cutList = Core.Services.Get<CutList>();

        /// <summary>Raised after a job was accepted, completed, delivered or more stock was bought.</summary>
        public event Action Changed;

        private void Awake()
        {
            if (saw == null || economy == null)
            {
                Debug.LogError($"{nameof(JobBoard)}: Saw and Economy must be assigned.", this);
                enabled = false;
                return;
            }

            kerf = saw.ToDomain().Kerf;
            templates = new JobTemplateInfo[jobs.Length];
            for (int i = 0; i < jobs.Length; i++)
            {
                templates[i] = jobs[i].ToTemplate();
                MaterialPlan plan = jobs[i].ToDomain().Plan(kerf);
                if (!plan.IsFeasible)
                {
                    Debug.LogWarning($"[{nameof(JobBoard)}] Job '{jobs[i].Id}' can't be made from its stock: " +
                                     string.Join(" ", plan.Problems), this);
                }
            }

            offers = new JobOffer[slots];
            templateOf = new int[slots];
            domainJobs = new Job[slots];
            plans = new MaterialPlan[slots];
            for (int i = 0; i < slots; i++)
            {
                templateOf[i] = -1;
            }
        }

        // A new game: the first week's offers (a save, if any, replaces them right after).
        private void Start()
        {
            if (Array.TrueForAll(offers, offer => offer == null))
            {
                DrawOffers();
            }
        }

        /// <summary>New offers now, without closing a week (Editor test shortcut, D-057).</summary>
        public void RedrawOffers()
        {
            if (!HasActiveJob)
            {
                DrawOffers();
            }
        }

        /// <summary>A new week's offers for every sheet (D-054).</summary>
        private void DrawOffers()
        {
            IReadOnlyList<JobOffer> drawn = JobMarket.Generate(templates, CareerState, slots, random.NextDouble);
            for (int i = 0; i < slots; i++)
            {
                SetOffer(i, i < drawn.Count ? drawn[i] : null);
            }

            Changed?.Invoke();
        }

        private void SetOffer(int slot, JobOffer offer)
        {
            offers[slot] = offer;
            templateOf[slot] = offer != null ? Array.FindIndex(jobs, job => job != null && job.Id == offer.TemplateId) : -1;
            if (templateOf[slot] < 0)
            {
                offers[slot] = null;
                domainJobs[slot] = null;
                plans[slot] = null;
                return;
            }

            domainJobs[slot] = jobs[templateOf[slot]].ToDomain(offer);
            plans[slot] = domainJobs[slot].Plan(kerf);
        }

        private JobDefinition TemplateAt(int slot) =>
            templateOf != null && slot >= 0 && slot < templateOf.Length && templateOf[slot] >= 0 ? jobs[templateOf[slot]] : null;

        private JobTemplateInfo TemplateInfoAt(int slot) =>
            templateOf != null && slot >= 0 && slot < templateOf.Length && templateOf[slot] >= 0 ? templates[templateOf[slot]] : null;

        private void OnEnable()
        {
            AssemblyStation.AnyChanged += OnStationChanged;
            AssemblyStation.AnyCompleted += OnStationCompleted;
            AssemblyStation.LoadedChanged += OnStationsLoaded;
            TimberBoard.AnySurfaceChanged += OnStationChanged;
        }

        private void OnDisable()
        {
            AssemblyStation.AnyChanged -= OnStationChanged;
            AssemblyStation.AnyCompleted -= OnStationCompleted;
            AssemblyStation.LoadedChanged -= OnStationsLoaded;
            TimberBoard.AnySurfaceChanged -= OnStationChanged;
        }

        /// <summary>
        /// Where a job is built: the loaded station that knows its blueprint (the assembly table, a site outside, a
        /// customer location, D-046, D-056); null when that place isn't loaded.
        /// </summary>
        private AssemblyStation StationFor(int index)
        {
            if (!(TemplateAt(index) is JobDefinition template))
            {
                return null;
            }

            foreach (AssemblyStation each in AssemblyStation.All)
            {
                if (each != null && each.FindBlueprint(template.Blueprint.Id) != null)
                {
                    return each;
                }
            }

            return null;
        }

        /// <summary>The station of the active job, or null (none, or its place isn't loaded).</summary>
        private AssemblyStation Active => HasActiveJob ? StationFor(activeIndex) : null;

        private void OnStationChanged(AssemblyStation changed) => OnStationChanged();

        private void OnStationCompleted(AssemblyStation completed) => OnStationCompleted();

        // A scene with stations came or went: cached results may refer to the other place.
        // A place that loads while its job is active takes the blueprint and the bench note shows the cut list.
        private void OnStationsLoaded()
        {
            OnStationChanged();
            if (Active != null && Active.IsEmpty)
            {
                Active.TrySetBlueprint(TemplateAt(activeIndex).Blueprint);
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
                        FinishedParts(), inspected, firstTime, offers[activeIndex]?.SpeciesId != null);
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
            Active != null
                ? CutListProgress.Evaluate(ActivePlan, Active.Build, WorkshopEconomy.LooseTimber())
                : Array.Empty<CutListLineProgress>();

        /// <summary>
        /// Whether a loose piece fits a part of the active job that is not installed yet (D-052: the apprentice leaves
        /// such pieces where they are).
        /// </summary>
        public bool NeedsPiece(TimberPiece piece)
        {
            if (Active == null || piece == null)
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
            if (Active == null || !Active.TryGetComponent(out Workshop.SitePreparation site))
            {
                return false;
            }

            stage = site.CurrentStage(Active.Build);
            return true;
        }

        /// <summary>What a job needs, before accepting it: material plan and what has to be bought.</summary>
        public string OfferText(int index) =>
            plans != null && index >= 0 && index < slots && plans[index] != null
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
            if (index < 0 || index >= slots)
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
            JobDefinition template = TemplateAt(index);
            if (template == null)
            {
                return Loc.T("No job on this sheet this week", "Bu hafta bu kağıtta iş yok");
            }

            string jobName = template.DisplayName;
            JobOffer offer = offers[index];
            string at = template.Location != null ? $" ({template.Location.DisplayName})" : string.Empty;
            string who = string.IsNullOrEmpty(offer.Customer) ? jobName + at : $"{jobName}{at}, {offer.Customer}";
            if (offer.IsLocked)
            {
                string reason = TemplateInfoAt(index).LockReason(CareerState) ?? offer.LockReason;
                return Loc.T($"Not yet: {jobName} ({offer.Customer}, pay {offer.Pay}): {reason}",
                    $"Henüz değil: {jobName} ({offer.Customer}, ücret {offer.Pay}): {reason}");
            }

            if (!HasActiveJob)
            {
                if (!plans[index].IsFeasible)
                {
                    return Loc.T($"{jobName}: can't be made from the stock (see Console)", $"{jobName}: bu malzemeden yapılamaz (Console'a bak)");
                }

                if (StationFor(index) is AssemblyStation place && !place.IsEmpty)
                {
                    return Loc.T($"Accept job: {jobName}   (clear the {place.DisplayName} first)",
                        $"İşi kabul et: {jobName}   (önce {place.DisplayName} boşaltılmalı)");
                }

                Money pay = domainJobs[index].Pay;
                Money material = Shopping(index).TotalCost;
                Money cost = material + PlaceExtra(template.Location, material);
                Money deposit = domainJobs[index].Deposit;
                string depositText = deposit > Money.Zero ? Loc.T($", deposit {deposit} now", $", avans {deposit} hemen") : string.Empty;
                return Loc.T($"Accept job: {who}   (pay {pay}{depositText}; material to buy {cost}, money {economy.Ledger.Balance})",
                    $"İşi kabul et: {who}   (ücret {pay}{depositText}; alınacak malzeme {cost}, para {economy.Ledger.Balance})");
            }

            if (index != activeIndex)
            {
                return Loc.T($"Job in progress: {TemplateAt(activeIndex).DisplayName}", $"Devam eden iş: {TemplateAt(activeIndex).DisplayName}");
            }

            if (ActiveJobElsewhere)
            {
                string place = ActiveJobPlace != null ? ActiveJobPlace.DisplayName : jobName;
                return Loc.T($"{jobName} is built at {place}: drive there with the van (E here: one more board delivered there)",
                    $"{jobName} {place} yerinde yapılıyor: kamyonetle git (burada E: oraya 1 tahta daha)");
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
            if (index < 0 || index >= slots || TemplateAt(index) == null)
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
            if (offers[index].IsLocked || !plans[index].IsFeasible || (StationFor(index) is AssemblyStation busy && !busy.IsEmpty))
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

            // A customer's place may cost more to supply (island: dearer, a delivery fee, D-056).
            World.LocationDefinition place = TemplateAt(index).Location;
            Money extra = PlaceExtra(place, shopping.TotalCost);

            // A deposit from the customer (D-046) helps to pay for the material.
            Money deposit = domainJobs[index].Deposit;
            if (economy.Ledger.Balance + deposit < shopping.TotalCost + extra)
            {
                ShowMessage(Loc.T($"Not enough money for the material ({shopping.TotalCost + extra})",
                    $"Malzeme için para yetmiyor ({shopping.TotalCost + extra})"));
                return;
            }

            // A place that isn't loaded takes the blueprint when it loads (OnStationsLoaded, D-056).
            if (StationFor(index) is AssemblyStation target && !target.TrySetBlueprint(TemplateAt(index).Blueprint))
            {
                return;
            }

            if (deposit > Money.Zero)
            {
                economy.Ledger.Earn(deposit, Loc.T($"Deposit: {TemplateAt(index).DisplayName}", $"Avans: {TemplateAt(index).DisplayName}"));
            }

            depositPaid = deposit;
            // The merchant brings a customer's material to the customer's place (D-056).
            economy.DeliveryOverride = place != null ? place.Id : null;
            foreach (ShoppingLine line in shopping.Lines)
            {
                if (line.UnitsToBuy > 0)
                {
                    economy.Buy(line.Item, line.UnitsToBuy);
                }
            }
            economy.DeliveryOverride = null;
            if (extra > Money.Zero)
            {
                economy.Ledger.TrySpend(extra, Loc.T($"Delivery to {place.DisplayName}", $"Teslimat: {place.DisplayName}"));
            }


            activeIndex = index;
            ElapsedSeconds = 0.0;
            // A new job on an outdoor site starts with no stage approved (D-053).
            if (StationFor(index) is AssemblyStation siteStation && siteStation.TryGetComponent(out Workshop.SitePreparation site))
            {
                site.ResetInspections();
            }

            boardsDelivered = plans[index].BoardsNeeded; // the boards set aside for the job, bought or from stock
            boardsOrderedExtra = 0;
            sawdustAtStart = waste != null ? waste.Log.TotalVolumeCubicMeters : 0.0;
            OnStationChanged();
            ShowCutList();
            ShowMessage(shopping.TotalCost > Money.Zero
                ? (!(StationFor(index) is AssemblyStation outdoor && outdoor.TryGetComponent(out Workshop.SitePreparation _))
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

            // An extra board for a job at a customer's place goes there too (D-056).
            World.LocationDefinition place = TemplateAt(activeIndex).Location;
            economy.DeliveryOverride = place != null ? place.Id : null;
            PurchaseResult result = economy.Buy(board, 1);
            economy.DeliveryOverride = null;
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

        /// <summary>Where the active job is built (a location id), or null without a job (D-056).</summary>
        public string ActiveJobLocation => HasActiveJob ? TemplateAt(activeIndex).LocationId : null;

        /// <summary>The place of the active job, or null (no job, or the workshop and its yard).</summary>
        public World.LocationDefinition ActiveJobPlace => HasActiveJob ? TemplateAt(activeIndex).Location : null;

        /// <summary>
        /// Hands the finished structure over at the customer's place (D-056: the job sheet is in the workshop, the
        /// structure at the customer's); false if it isn't complete or not here.
        /// </summary>
        public bool TryDeliverHere()
        {
            if (!IsComplete)
            {
                return false;
            }

            Deliver();
            return true;
        }

        /// <summary>The last message of the job service (shown by the sheets and the customer).</summary>
        public string LastMessage => lastMessage != null && Time.unscaledTime < lastMessageUntil ? lastMessage : null;

        /// <summary>A customer's place costs more to supply: the material price factor and the merchant's fee.</summary>
        private static Money PlaceExtra(World.LocationDefinition place, Money material)
        {
            if (place == null || material <= Money.Zero)
            {
                return Money.Zero;
            }

            return material.Scale(place.MaterialPriceFactor - 1.0) + place.DeliveryFee;
        }

        /// <summary>The finished structure goes to the customer: the job is paid, the station is cleared.</summary>
        private void Deliver()
        {
            JobSettlement settlement = Settlement;
            string jobName = TemplateAt(activeIndex).DisplayName;
            // The deposit was paid on acceptance; the rest now (the deposit never exceeds the delivery share).
            Money rest = settlement.Earned - depositPaid;
            if (rest > Money.Zero)
            {
                economy.Ledger.Earn(rest, Loc.T($"Job: {jobName}", $"İş: {jobName}"));
            }

            depositPaid = Money.Zero;
            completedJobIds.Add(TemplateAt(activeIndex).Id);

            // The career moves on and the week closes (D-054): experience, reputation, skills, rent and wage.
            JobResult result = Result;
            AssemblyQualityReport report = result.Report;
            string careerText = string.Empty;
            if (career != null)
            {
                JobOutcome outcome = new JobOutcome(SizeOf(activeIndex), result.GoalsMet, result.Goals.Count,
                    report.PartsRequired, report.FastenersRequired, ActiveJob.RequiresFinish,
                    Active.TryGetComponent(out Workshop.SitePreparation _));
                JobReview review = career.CloseWeek(outcome);
                careerText = Loc.T(
                    $"   +{review.Experience} XP, reputation {review.ReputationBefore:0.0} > {review.ReputationAfter:0.0}, week costs {career.LastCosts}",
                    $"   +{review.Experience} XP, itibar {review.ReputationBefore:0.0} > {review.ReputationAfter:0.0}, hafta gideri {career.LastCosts}");
                if (review.RankedUp)
                {
                    careerText += Loc.T($"\nNew rank: {CareerNames.Rank(review.RankAfter)}!",
                        $"\nYeni seviye: {CareerNames.Rank(review.RankAfter)}!");
                }
            }

            // The site is ready for the next customer: no stage approved (D-053, D-056).
            if (Active.TryGetComponent(out Workshop.SitePreparation finishedSite))
            {
                finishedSite.ResetInspections();
            }

            Active.ClearAll();
            activeIndex = -1;
            OnStationChanged();
            if (NoteOnBench != null)
            {
                NoteOnBench.ClearJobLines();
            }

            // A new week: new offers on the board.
            DrawOffers();
            ShowMessage(Loc.T($"Delivered: +{settlement.Earned} (profit {settlement.Profit}){careerText}",
                $"Teslim edildi: +{settlement.Earned} (kâr {settlement.Profit}){careerText}"), 8f);
            Changed?.Invoke();
        }

        private void ShowMessage(string message, float seconds = 4f)
        {
            lastMessage = message;
            lastMessageUntil = Time.unscaledTime + seconds;
        }

        private void ShowCutList()
        {
            if (NoteOnBench == null || !HasActiveJob)
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

            NoteOnBench.SetJobLines(lengths, labels);
        }

        /// <summary>
        /// The size of the job on a sheet for experience (D-054): its pay with the wood and finish asked for, without the
        /// reputation factor (better reputation pays more, but doesn't make the job bigger).
        /// </summary>
        private Money SizeOf(int slot)
        {
            JobTemplateInfo template = TemplateInfoAt(slot);
            JobOffer offer = offers[slot];
            SpeciesOption wood = null;
            foreach (SpeciesOption option in template.SpeciesOptions)
            {
                if (option.SpeciesId == offer.SpeciesId)
                {
                    wood = option;
                }
            }

            return JobMarket.PayFor(template, wood, offer.RequiresFinish, 1.0);
        }

        public OfferData[] CaptureOffers()
        {
            var data = new OfferData[slots];
            for (int i = 0; i < slots; i++)
            {
                JobOffer offer = offers[i];
                data[i] = offer == null
                    ? new OfferData { templateId = string.Empty }
                    : new OfferData
                    {
                        templateId = offer.TemplateId, customer = offer.Customer, speciesId = offer.SpeciesId ?? string.Empty,
                        requiresFinish = offer.RequiresFinish, payCents = offer.Pay.Cents, locked = offer.IsLocked
                    };
            }

            return data;
        }

        public CareerData CaptureCareer() => career != null ? career.Capture() : null;

        /// <summary>
        /// The saved offers; an older save has none: new ones are drawn, and its active job (by template) gets the first
        /// sheet with the template's own wood and pay.
        /// </summary>
        private void RestoreOffers(OfferData[] saved, JobData data)
        {
            bool any = false;
            for (int i = 0; i < slots; i++)
            {
                OfferData entry = saved != null && i < saved.Length ? saved[i] : null;
                JobOffer offer = null;
                if (entry != null && !string.IsNullOrEmpty(entry.templateId))
                {
                    int template = Array.FindIndex(jobs, job => job != null && job.Id == entry.templateId);
                    string lockReason = entry.locked && template >= 0
                        ? templates[template].LockReason(CareerState) ?? Loc.T("locked", "kilitli")
                        : null;
                    offer = new JobOffer(entry.templateId, entry.customer, string.IsNullOrEmpty(entry.speciesId) ? null : entry.speciesId,
                        entry.requiresFinish, Money.FromCents(entry.payCents), lockReason);
                    any = true;
                }

                SetOffer(i, offer);
            }

            if (any)
            {
                return;
            }

            DrawOffers();
            if (!string.IsNullOrEmpty(data.activeJobId))
            {
                int template = Array.FindIndex(jobs, job => job != null && job.Id == data.activeJobId);
                if (template >= 0)
                {
                    SetOffer(0, new JobOffer(jobs[template].Id, string.Empty, null, false, templates[template].BasePay));
                    data.activeSlot = 0;
                }
            }
        }

        /// <summary>Save state of the job system.</summary>
        public JobData Capture() => new JobData
        {
            activeJobId = HasActiveJob ? TemplateAt(activeIndex).Id : string.Empty,
            activeSlot = activeIndex,
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
        public void Restore(JobData data, OfferData[] savedOffers = null, CareerData savedCareer = null)
        {
            completedJobIds.Clear();
            completedJobIds.AddRange(data.completedJobIds ?? Array.Empty<string>());

            // The career first: the offers' locks depend on it. An older save starts it from the delivered jobs.
            if (career != null)
            {
                var pays = new List<Money>();
                foreach (string id in completedJobIds)
                {
                    int template = Array.FindIndex(jobs, job => job != null && job.Id == id);
                    if (template >= 0)
                    {
                        pays.Add(templates[template].BasePay);
                    }
                }

                career.Restore(savedCareer, pays);
            }

            RestoreOffers(savedOffers, data);
            activeIndex = -1;
            OnStationChanged();
            if (!string.IsNullOrEmpty(data.activeJobId))
            {
                activeIndex = data.activeSlot >= 0 && data.activeSlot < slots && TemplateAt(data.activeSlot)?.Id == data.activeJobId
                    ? data.activeSlot
                    : -1;
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
                StationFor(activeIndex)?.TrySetBlueprint(TemplateAt(activeIndex).Blueprint);
                ShowCutList();
            }

            Changed?.Invoke();
        }
    }
}
