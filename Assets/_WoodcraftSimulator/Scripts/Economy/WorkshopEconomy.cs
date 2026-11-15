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
        [SerializeField] private StockDelivery delivery;

        [Tooltip("Optional: delivery area at the outdoor site, for catalog boards marked 'deliver outdoors' (D-046).")]
        [SerializeField] private StockDelivery outdoorDelivery;

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
            if (catalog == null || delivery == null)
            {
                Debug.LogError($"{nameof(WorkshopEconomy)}: Catalog and Delivery must be assigned.", this);
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
                (DeliversOutdoors(item) ? outdoorDelivery : delivery).Deliver(species, item.BoardStock.Dimensions, quantity);
            }

            return result;
        }

        /// <summary>Whether a bought board goes to the outdoor site rather than the workshop.</summary>
        public bool DeliversOutdoors(CatalogItem item) =>
            item != null && item.Kind == CatalogItemKind.Board && outdoorDelivery != null && catalog.DeliversOutdoors(item.Id);

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

            return loose;
        }

        /// <summary>The inventory screen text (money, timber by species and section, fasteners).</summary>
        public string InventoryText() =>
            WorkshopInventory.Format(Ledger, WorkshopInventory.Summarize(LooseTimber(), Catalog), Fasteners, FastenerName);

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
