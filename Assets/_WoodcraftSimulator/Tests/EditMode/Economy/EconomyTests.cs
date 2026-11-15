using System;
using System.Collections.Generic;
using NUnit.Framework;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Economy
{
    public sealed class EconomyTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly StockSpec ShelfBoard = new StockSpec("spruce", "Spruce", Size(2000));
        private const string ScrewId = "wood-screw-4x40";

        private static BoardDimensions Size(double length) => BoardDimensions.FromMillimeters(length, 145, 18);

        private static Money Eur(decimal euros) => Money.FromEuros(euros);

        private static MaterialCatalog Catalog() => new MaterialCatalog(new[]
        {
            CatalogItem.Board("spruce-2000x145x18", ShelfBoard, Eur(8.50m)),
            CatalogItem.FastenerBox("screws-4x40-200", "Box of 200 wood screws 4 x 40", ScrewId, 200, Eur(6.00m)),
            CatalogItem.FastenerBox("screws-4x40-50", "Box of 50 wood screws 4 x 40", ScrewId, 50, Eur(2.00m))
        });

        [Test]
        public void Money_FormatsAndRounds()
        {
            Assert.That(Eur(12.5m).ToString(), Is.EqualTo("€12.50"));
            Assert.That((-Eur(3.05m)).ToString(), Is.EqualTo("-€3.05"));
            Assert.That(Money.Zero.ToString(), Is.EqualTo("€0.00"));
            Assert.That(Eur(0.005m).Cents, Is.EqualTo(1));
            Assert.That(Eur(8.50m).Scale(0.5).Cents, Is.EqualTo(425));
            Assert.That(Money.FromCents(5).Scale(0.5).Cents, Is.EqualTo(3));
            Assert.That(Eur(1m) * 3 - Eur(0.5m), Is.EqualTo(Eur(2.5m)));
        }

        [Test]
        public void Ledger_RefusesPurchasesItCannotPay()
        {
            var ledger = new Ledger();
            ledger.Start(Eur(20m));

            Assert.That(ledger.TrySpend(Eur(17m), "2 boards"), Is.True);
            Assert.That(ledger.TrySpend(Eur(3.01m), "too much"), Is.False);
            Assert.That(ledger.Balance, Is.EqualTo(Eur(3m)));

            ledger.Earn(Eur(60m), "Wall shelf");
            Assert.That(ledger.Balance, Is.EqualTo(Eur(63m)));
            Assert.That(ledger.TotalJobIncome, Is.EqualTo(Eur(60m)));
            Assert.That(ledger.TotalPurchases, Is.EqualTo(Eur(17m)));
            Assert.That(ledger.Entries.Count, Is.EqualTo(3));
            Assert.Throws<InvalidOperationException>(() => ledger.Start(Eur(1m)));
        }

        [Test]
        public void Ledger_Restore_RejectsNegativeBalance()
        {
            var ledger = new Ledger();
            Assert.Throws<ArgumentException>(() => ledger.Restore(new[]
            {
                new Transaction(TransactionKind.StartingFunds, Eur(5m), "Start"),
                new Transaction(TransactionKind.Purchase, -Eur(6m), "Board")
            }));
            Assert.That(ledger.IsStarted, Is.False);
        }

        [Test]
        public void FastenerStock_UsesOnlyWhatItHas()
        {
            var stock = new FastenerStock();
            stock.Add(ScrewId, 2);

            Assert.That(stock.TryUse(ScrewId), Is.True);
            Assert.That(stock.TryUse(ScrewId, 2), Is.False);
            Assert.That(stock.TryUse(ScrewId), Is.True);
            Assert.That(stock.TryUse(ScrewId), Is.False);
            Assert.That(stock.All.Count, Is.EqualTo(0));
        }

        [Test]
        public void Catalog_FindsItems_AndCheapestBox()
        {
            MaterialCatalog catalog = Catalog();

            Assert.That(catalog.FindBoard(ShelfBoard).Id, Is.EqualTo("spruce-2000x145x18"));
            Assert.That(catalog.FindBoard("spruce", Size(1999)), Is.Null);
            Assert.That(catalog.FindBoardOfSection("spruce", Size(600)).Id, Is.EqualTo("spruce-2000x145x18"));

            // 3 cents per screw in the big box, 4 in the small one.
            Assert.That(catalog.FindFastenerBox(ScrewId).Id, Is.EqualTo("screws-4x40-200"));
            Assert.That(catalog.FindFastenerBox("nail"), Is.Null);

            Assert.Throws<ArgumentException>(() => new MaterialCatalog(new[]
            {
                CatalogItem.Board("x", ShelfBoard, Eur(1m)), CatalogItem.Board("x", ShelfBoard, Eur(2m))
            }));
        }

        [Test]
        public void Catalog_ProratesBoardsAndScrews()
        {
            MaterialCatalog catalog = Catalog();
            CatalogItem board = catalog.FindBoard(ShelfBoard);

            Assert.That(board.ValueOfVolume(ShelfBoard.Dimensions.VolumeCubicMeters), Is.EqualTo(Eur(8.50m)));
            Assert.That(board.ValueOfVolume(Size(600).VolumeCubicMeters), Is.EqualTo(Eur(2.55m)));
            Assert.That(catalog.FindFastenerBox(ScrewId).ValueOfFasteners(12), Is.EqualTo(Eur(0.36m)));
        }

        [Test]
        public void TryBuy_PaysAndAddsFastenerBoxes()
        {
            MaterialCatalog catalog = Catalog();
            var ledger = new Ledger();
            ledger.Start(Eur(20m));
            var fasteners = new FastenerStock();

            Assert.That(MaterialCatalog.TryBuy(ledger, catalog.Find("screws-4x40-200"), 1, fasteners), Is.EqualTo(PurchaseResult.Bought));
            Assert.That(fasteners.Count(ScrewId), Is.EqualTo(200));

            Assert.That(MaterialCatalog.TryBuy(ledger, catalog.FindBoard(ShelfBoard), 2, fasteners), Is.EqualTo(PurchaseResult.NotEnoughMoney));
            Assert.That(MaterialCatalog.TryBuy(ledger, catalog.FindBoard(ShelfBoard), 1, fasteners), Is.EqualTo(PurchaseResult.Bought));
            Assert.That(MaterialCatalog.TryBuy(ledger, catalog.FindBoard(ShelfBoard), 0, fasteners), Is.EqualTo(PurchaseResult.InvalidQuantity));
            Assert.That(ledger.Balance, Is.EqualTo(Eur(5.50m)));
            Assert.That(ledger.Entries[2].Description, Is.EqualTo("1 x Spruce board 2000 × 145 × 18 mm"));
        }

        [Test]
        public void Inventory_GroupsBoardsAndCutPieces()
        {
            var pieces = new List<TimberPiece>
            {
                new TimberPiece(Spruce, Size(2000)),
                new TimberPiece(Spruce, Size(330)),
                new TimberPiece(Spruce, Size(2000)),
                new TimberPiece(Spruce, Size(600)),
                new TimberPiece(Spruce, Size(600)),
                new TimberPiece(Spruce, BoardDimensions.FromMillimeters(2400, 95, 45))
            };

            IReadOnlyList<InventoryGroup> groups = WorkshopInventory.Summarize(pieces, Catalog());

            Assert.That(groups.Count, Is.EqualTo(2));
            InventoryGroup shelf = groups[0];
            Assert.That(shelf.Width, Is.EqualTo(Length.FromMillimeters(145)));
            Assert.That(shelf.StockBoards.Count, Is.EqualTo(2));
            Assert.That(shelf.CutPieces, Is.EqualTo(new[]
            {
                Length.FromMillimeters(600), Length.FromMillimeters(600), Length.FromMillimeters(330)
            }));
            Assert.That(shelf.Value, Is.EqualTo(Eur(8.50m) * 2 + Eur(2.55m) * 2 + Eur(1.40m)));
            Assert.That(groups[1].Value, Is.Null);
            Assert.That(WorkshopInventory.CountStockBoards(pieces, ShelfBoard), Is.EqualTo(2));

            var ledger = new Ledger();
            ledger.Start(Eur(100m));
            var fasteners = new FastenerStock();
            fasteners.Add(ScrewId, 188);
            string text = WorkshopInventory.Format(ledger, groups, fasteners, id => "Wood screw 4 x 40 mm");
            StringAssert.Contains("Money: €100.00", text);
            StringAssert.Contains("Spruce 145 × 18 mm:   value €23.50", text);
            StringAssert.Contains("boards: 2 x 2000 mm", text);
            StringAssert.Contains("cut pieces: 2 x 600, 330 mm", text);
            StringAssert.Contains("188 x Wood screw 4 x 40 mm", text);
        }
    }
}
