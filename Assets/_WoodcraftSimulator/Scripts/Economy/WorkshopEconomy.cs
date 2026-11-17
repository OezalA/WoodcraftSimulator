using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Persistence;

namespace WoodcraftSimulator.Economy
{
    /// <summary>
    /// The workshop's money, fastener stock and the merchant's catalog (Phase 7b, D-040). A new workshop starts with
    /// the starting funds and fasteners; a save restores them (<see cref="SaveGameService"/>). Boards bought are
    /// delivered to the <see cref="StockDelivery"/>; fastener boxes go straight into the stock.
    /// </summary>
    public sealed class WorkshopEconomy : MonoBehaviour
    {
        [Serializable]
        public sealed class StartingFasteners
        {
            public FastenerDefinition fastener;
            [Min(0)] public int count = 200;
        }

        [SerializeField] private MaterialCatalogDefinition catalog;

        // The economy lives in the core scene; the delivery areas in the content scenes, found by id (D-056).
        public const string WorkshopDelivery = "workshop";
        public const string OutdoorDelivery = "outdoor";

        /// <summary>Boards bought for a place that isn't loaded: delivered when it loads (D-056).</summary>
        [Serializable]
        public sealed class PendingDelivery
        {
            public string deliveryId;
            public string speciesId;
            public float lengthMillimeters;
            public float widthMillimeters;
            public float thicknessMillimeters;
            public int count;
        }

        private readonly List<PendingDelivery> pending = new List<PendingDelivery>();

        public IReadOnlyList<PendingDelivery> Pending => pending;

        /// <summary>
        /// Where boards for the active job go instead of the usual area (a customer location, D-056); null = the usual.
        /// </summary>
        public string DeliveryOverride { get; set; }

        [Tooltip("Fastener types (display names in the inventory).")]
        [SerializeField] private FastenerDefinition[] fasteners = Array.Empty<FastenerDefinition>();

        [Header("New workshop")]
        [SerializeField, Min(0f)] private float startingFundsEuros = 100f;
        [SerializeField] private StartingFasteners[] startingFasteners = Array.Empty<StartingFasteners>();

        public Ledger Ledger { get; } = new Ledger();

        public FastenerStock Fasteners { get; } = new FastenerStock();

        public MaterialCatalog Catalog { get; private set; }

        /// <summary>Raised after money or fasteners changed.</summary>
        public event Action Changed;

        private void Awake()
        {
            if (catalog == null)
            {
                Debug.LogError($"{nameof(WorkshopEconomy)}: Catalog must be assigned.", this);
                enabled = false;
                return;
            }

            Catalog = catalog.ToDomain();
            Ledger.Changed += RaiseChanged;
            Fasteners.Changed += RaiseChanged;
        }

        private void Start()
        {
            // A save loaded in SaveGameService.Start may already have restored the ledger.
            EnsureStarted();
        }

        /// <summary>Gives the starting funds and fasteners if the ledger was never started (new workshop, old save).</summary>
        public void EnsureStarted()
        {
            if (Ledger.IsStarted)
            {
                return;
            }

            Ledger.Start(Money.FromEuros((decimal)Math.Round(startingFundsEuros, 2)));
            foreach (StartingFasteners entry in startingFasteners)
            {
                if (entry?.fastener != null && entry.count > 0)
                {
                    Fasteners.Add(entry.fastener.Id, entry.count);
                }
            }
        }

        /// <summary>Pays and delivers. Nothing happens unless the result is <see cref="PurchaseResult.Bought"/>.</summary>
        public PurchaseResult Buy(CatalogItem item, int quantity)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            WoodSpeciesDefinition species = item.Kind == CatalogItemKind.Board ? catalog.SpeciesOf(item.Id) : null;
            if (item.Kind == CatalogItemKind.Board && species == null)
            {
                Debug.LogError($"[{nameof(WorkshopEconomy)}] Board item '{item.Id}' has no species asset.", this);
                return PurchaseResult.InvalidQuantity;
            }

            PurchaseResult result = MaterialCatalog.TryBuy(Ledger, item, quantity, Fasteners);
            if (result == PurchaseResult.Bought && species != null)
            {
                string target = DeliveryOverride ?? (DeliversOutdoors(item) ? OutdoorDelivery : WorkshopDelivery);
                StockDelivery area = StockDelivery.Find(target);
                if (area != null)
                {
                    area.Deliver(species, item.BoardStock.Dimensions, quantity);
                }
                else
                {
                    // The place isn't loaded (e.g. a customer location): the boards wait there for the player.
                    BoardDimensions size = item.BoardStock.Dimensions;
                    pending.Add(new PendingDelivery
                    {
                        deliveryId = target, speciesId = species.Id, count = quantity,
                        lengthMillimeters = (float)size.Length.Millimeters, widthMillimeters = (float)size.Width.Millimeters,
                        thicknessMillimeters = (float)size.Thickness.Millimeters
                    });
                }
            }

            return result;
        }

        /// <summary>Brings the boards waiting for this area (its scene just loaded).</summary>
        public void DeliverPending(StockDelivery area)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingDelivery order = pending[i];
                if (order.deliveryId != area.DeliveryId)
                {
                    continue;
                }

                WoodSpeciesDefinition species = catalog != null ? catalog.SpeciesById(order.speciesId) : null;
                if (species != null)
                {
                    area.Deliver(species, BoardDimensions.FromMillimeters(order.lengthMillimeters, order.widthMillimeters,
                        order.thicknessMillimeters), order.count);
                }

                pending.RemoveAt(i);
            }
        }

        public void RestorePending(IEnumerable<PendingDelivery> orders)
        {
            pending.Clear();
            pending.AddRange(orders ?? Array.Empty<PendingDelivery>());
        }

        /// <summary>Whether a bought board goes to the outdoor site rather than the workshop.</summary>
        public bool DeliversOutdoors(CatalogItem item) =>
            item != null && item.Kind == CatalogItemKind.Board && catalog.DeliversOutdoors(item.Id);

        /// <summary>Where a bought item ends up, for messages.</summary>
        public string DeliveryText(CatalogItem item) => item.Kind != CatalogItemKind.Board
            ? Loc.T("the fasteners are in the stock", "bağlantı elemanları stokta")
            : DeliversOutdoors(item)
                ? Loc.T("delivered to the terrace site outside", "dışarıdaki teras şantiyesine bırakıldı")
                : Loc.T("delivered to the delivery area", "teslim alanına bırakıldı");

        /// <summary>Uses one fastener of this type for a driven screw or nail; false if none is left.</summary>
        public bool TryUseFastener(string fastenerId) => Fasteners.TryUse(fastenerId);

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

        /// <summary>Timber pieces lying in the workshop (not installed in an assembly).</summary>
        public static List<TimberPiece> LooseTimber()
        {
            var loose = new List<TimberPiece>();
            foreach (TimberBoard board in FindObjectsByType<TimberBoard>(FindObjectsSortMode.None))
            {
                if (board.Piece != null && (!board.TryGetComponent(out Carryable carryable) || carryable.InstalledIn == null))
                {
                    loose.Add(board.Piece);
                }
            }

            // Offcuts put away in the bins are still the workshop's (D-052).
            foreach (Workshop.OffcutBin bin in Workshop.OffcutBin.All)
            {
                foreach (StoredPiece stored in bin.Stock.Pieces)
                {
                    loose.Add(stored.Piece);
                }
            }

            return loose;
        }

        /// <summary>The inventory screen text (money, timber by species and section, fasteners).</summary>
        public string InventoryText()
        {
            string text = WorkshopInventory.Format(Ledger, WorkshopInventory.Summarize(LooseTimber(), Catalog), Fasteners, FastenerName);
            Career.WorkshopCareer career = Career.WorkshopCareer.Active;
            if (career == null)
            {
                return text;
            }

            // Running costs (D-054): what a week costs, what was paid so far, and a debt warning.
            Money weekly = Career.RunningCosts.Weekly(career.ApprenticeHired);
            string wage = career.ApprenticeHired
                ? Loc.T($" + wage {Career.RunningCosts.ApprenticeWage}", $" + maaş {Career.RunningCosts.ApprenticeWage}")
                : string.Empty;
            text += Loc.T(
                $"\nWeek {career.Progress.Week}: costs {weekly} a week (rent {Career.RunningCosts.Rent}{wage}), paid when a job is delivered; paid so far {Ledger.TotalRunningCosts}",
                $"\n{career.Progress.Week}. hafta: haftalık gider {weekly} (kira {Career.RunningCosts.Rent}{wage}), iş teslim edilince ödenir; şimdiye kadar {Ledger.TotalRunningCosts}");
            if (Ledger.IsInDebt)
            {
                text += Loc.T("\nIn debt: nothing can be bought until a job pays (jobs with a deposit help)",
                    "\nBorçtasın: bir iş ödeyene kadar satın alma yok (avanslı işler yardım eder)");
            }

            return text;
        }

        public static string PurchaseText(PurchaseResult result) => result switch
        {
            PurchaseResult.NotEnoughMoney => Loc.T("Not enough money", "Para yetmiyor"),
            PurchaseResult.InvalidQuantity => Loc.T("Can't be ordered", "Sipariş edilemez"),
            _ => Loc.T("Bought", "Satın alındı")
        };

        public EconomyData Capture() => SaveFormat.ToData(Ledger, Fasteners);

        /// <summary>Loading a save: restores money and fasteners, or starts the ledger for a save made before the economy.</summary>
        public void Restore(EconomyData data)
        {
            if (!SaveFormat.TryRestore(data, Ledger, Fasteners))
            {
                EnsureStarted();
            }
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
