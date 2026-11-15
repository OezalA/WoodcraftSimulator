using System;
using System.Collections.Generic;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Economy
{
    public enum CatalogItemKind
    {
        /// <summary>One stock board (a physical object delivered to the workshop).</summary>
        Board,

        /// <summary>A box of fasteners (added to the workshop's <see cref="FastenerStock"/>).</summary>
        FastenerBox
    }

    /// <summary>Something the timber merchant sells, with its unit price (master prompt Phase 7).</summary>
    public sealed class CatalogItem
    {
        private CatalogItem(string id, string displayName, CatalogItemKind kind, Money unitPrice, StockSpec board,
            string fastenerId, int boxSize)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Catalog id must not be empty.", nameof(id));
            if (unitPrice <= Money.Zero) throw new ArgumentOutOfRangeException(nameof(unitPrice), "Price must be positive.");

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Kind = kind;
            UnitPrice = unitPrice;
            BoardStock = board;
            FastenerId = fastenerId;
            BoxSize = boxSize;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public CatalogItemKind Kind { get; }

        public Money UnitPrice { get; }

        /// <summary>The board sold (<see cref="CatalogItemKind.Board"/> only).</summary>
        public StockSpec BoardStock { get; }

        /// <summary>The fastener in the box (<see cref="CatalogItemKind.FastenerBox"/> only).</summary>
        public string FastenerId { get; }

        /// <summary>Fasteners per box (<see cref="CatalogItemKind.FastenerBox"/> only).</summary>
        public int BoxSize { get; }

        public static CatalogItem Board(string id, StockSpec stock, Money price)
        {
            if (stock == null) throw new ArgumentNullException(nameof(stock));
            if (stock.Dimensions.VolumeCubicMeters <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(stock), "A board must have a volume.");
            }

            return new CatalogItem(id, stock.ToString(), CatalogItemKind.Board, price, stock, null, 0);
        }

        public static CatalogItem FastenerBox(string id, string displayName, string fastenerId, int boxSize, Money price)
        {
            if (string.IsNullOrWhiteSpace(fastenerId)) throw new ArgumentException("Fastener id must not be empty.", nameof(fastenerId));
            if (boxSize <= 0) throw new ArgumentOutOfRangeException(nameof(boxSize), boxSize, "A box must hold fasteners.");

            return new CatalogItem(id, displayName, CatalogItemKind.FastenerBox, price, null, fastenerId, boxSize);
        }

        /// <summary>
        /// Value of <paramref name="cubicMeters"/> of this board's timber, prorated from the board price
        /// (owner's choice K3: a job pays for the material it used). Board items only.
        /// </summary>
        public Money ValueOfVolume(double cubicMeters)
        {
            RequireKind(CatalogItemKind.Board);
            if (!(cubicMeters >= 0.0) || double.IsInfinity(cubicMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(cubicMeters), cubicMeters, "Volume must be zero or positive.");
            }

            return UnitPrice.Scale(cubicMeters / BoardStock.Dimensions.VolumeCubicMeters);
        }

        /// <summary>Value of <paramref name="count"/> fasteners taken from a box. Box items only.</summary>
        public Money ValueOfFasteners(int count)
        {
            RequireKind(CatalogItemKind.FastenerBox);
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Count must not be negative.");

            return UnitPrice.Scale((double)count / BoxSize);
        }

        public override string ToString() => $"{DisplayName} ({UnitPrice})";

        private void RequireKind(CatalogItemKind kind)
        {
            if (Kind != kind)
            {
                throw new InvalidOperationException($"'{Id}' is a {Kind}, not a {kind}.");
            }
        }
    }

    /// <summary>Why a purchase was refused.</summary>
    public enum PurchaseResult
    {
        Bought,
        InvalidQuantity,
        NotEnoughMoney
    }

    /// <summary>What the timber merchant sells. Ids are unique.</summary>
    public sealed class MaterialCatalog
    {
        /// <summary>Largest quantity of one item per order (a sanity limit for the delivery area).</summary>
        public const int MaximumQuantity = 20;

        private readonly List<CatalogItem> items;

        public MaterialCatalog(IEnumerable<CatalogItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            this.items = new List<CatalogItem>();
            var ids = new HashSet<string>();
            foreach (CatalogItem item in items)
            {
                if (item == null) throw new ArgumentException("The catalog contains an empty item.", nameof(items));
                if (!ids.Add(item.Id)) throw new ArgumentException($"Catalog id '{item.Id}' is used twice.", nameof(items));

                this.items.Add(item);
            }
        }

        public IReadOnlyList<CatalogItem> Items => items;

        public CatalogItem Find(string id) => items.Find(item => item.Id == id);

        /// <summary>The board item for exactly this species and size, or null.</summary>
        public CatalogItem FindBoard(string speciesId, BoardDimensions dimensions) =>
            items.Find(item => item.Kind == CatalogItemKind.Board && item.BoardStock.Matches(speciesId, dimensions));

        public CatalogItem FindBoard(StockSpec stock) =>
            stock == null ? null : FindBoard(stock.SpeciesId, stock.Dimensions);

        /// <summary>
        /// The board item with this species and cross-section (any length), to value offcuts; prefers the longest
        /// board. Null if none.
        /// </summary>
        public CatalogItem FindBoardOfSection(string speciesId, BoardDimensions dimensions)
        {
            CatalogItem best = null;
            foreach (CatalogItem item in items)
            {
                if (item.Kind != CatalogItemKind.Board || item.BoardStock.SpeciesId != speciesId)
                {
                    continue;
                }

                BoardDimensions stock = item.BoardStock.Dimensions;
                if (stock.Width == dimensions.Width && stock.Thickness == dimensions.Thickness &&
                    (best == null || stock.Length > best.BoardStock.Dimensions.Length))
                {
                    best = item;
                }
            }

            return best;
        }

        /// <summary>The cheapest box (per fastener) of this fastener, or null.</summary>
        public CatalogItem FindFastenerBox(string fastenerId)
        {
            CatalogItem best = null;
            foreach (CatalogItem item in items)
            {
                if (item.Kind == CatalogItemKind.FastenerBox && item.FastenerId == fastenerId &&
                    (best == null || item.UnitPrice.Cents * best.BoxSize < best.UnitPrice.Cents * item.BoxSize))
                {
                    best = item;
                }
            }

            return best;
        }

        /// <summary>
        /// Pays for <paramref name="quantity"/> × <paramref name="item"/>. A fastener box is added to
        /// <paramref name="fasteners"/> at once; boards are delivered by the caller (they are physical objects).
        /// Nothing changes unless the result is <see cref="PurchaseResult.Bought"/>.
        /// </summary>
        public static PurchaseResult TryBuy(Ledger ledger, CatalogItem item, int quantity, FastenerStock fasteners)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Kind == CatalogItemKind.FastenerBox && fasteners == null)
            {
                throw new ArgumentNullException(nameof(fasteners), "Buying a fastener box needs the fastener stock.");
            }

            if (quantity < 1 || quantity > MaximumQuantity)
            {
                return PurchaseResult.InvalidQuantity;
            }

            if (!ledger.TrySpend(item.UnitPrice * quantity, $"{quantity} x {item.DisplayName}"))
            {
                return PurchaseResult.NotEnoughMoney;
            }

            if (item.Kind == CatalogItemKind.FastenerBox)
            {
                fasteners.Add(item.FastenerId, item.BoxSize * quantity);
            }

            return PurchaseResult.Bought;
        }
    }
}
