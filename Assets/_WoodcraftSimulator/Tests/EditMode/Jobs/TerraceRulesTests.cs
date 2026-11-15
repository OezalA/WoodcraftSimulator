using NUnit.Framework;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Jobs
{
    /// <summary>Several stocks per job, non-timber materials, connectors and deposits (D-046).</summary>
    public sealed class TerraceRulesTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly WoodSpecies Concrete = new WoodSpecies("concrete", "Concrete", 2200, isTimber: false);

        private static readonly StockSpec Blocks = new StockSpec("concrete", "Concrete", Dim(200, 200, 200));
        private static readonly StockSpec Beams = new StockSpec("spruce", "Spruce", Dim(2400, 145, 45));
        private static readonly StockSpec Joists = new StockSpec("spruce", "Spruce", Dim(2400, 120, 45));
        private static readonly StockSpec Decking = new StockSpec("spruce", "Spruce", Dim(2400, 145, 28));

        private static readonly FastenerSpec Bracket = new FastenerSpec("bracket", "Bracket", Length.FromMillimeters(5),
            Length.FromMillimeters(50), FastenerKind.Connector);

        private static Length Mm(double value) => Length.FromMillimeters(value);

        private static BoardDimensions Dim(double length, double width, double thickness) =>
            BoardDimensions.FromMillimeters(length, width, thickness);

        private static Blueprint MiniTerrace()
        {
            var slots = new[]
            {
                new PartSlot("pier-1", "Pier", Dim(200, 200, 200)), new PartSlot("pier-2", "Pier", Dim(200, 200, 200)),
                new PartSlot("beam", "Beam", Dim(2400, 145, 45), new[] { "pier-1", "pier-2" }),
                new PartSlot("joist-1", "Joist", Dim(1800, 120, 45)), new PartSlot("joist-2", "Joist", Dim(1800, 120, 45)),
                new PartSlot("deck-1", "Deck", Dim(2400, 145, 28))
            };
            var connections = new[]
            {
                new ConnectionSpec("pier-1-beam", "beam", "pier-1", Bracket.Id, 1, new[] { Mm(20) }),
                new ConnectionSpec("joist-1-beam", "joist-1", "beam", Bracket.Id, 1, new[] { Mm(20) })
            };
            return new Blueprint("t", "Terrace", slots, connections, new Tolerance(Mm(1)), new Tolerance(Mm(5)));
        }

        [Test]
        public void Plan_PicksTheFittingStockForEachPart()
        {
            MaterialPlan plan = MaterialPlan.Calculate(MiniTerrace(), new[] { Decking, Beams, Joists, Blocks }, Mm(2));

            Assert.That(plan.IsFeasible, Is.True, string.Join(" ", plan.Problems));
            Assert.That(plan.BoardsNeededFor(Blocks), Is.EqualTo(2));
            Assert.That(plan.BoardsNeededFor(Beams), Is.EqualTo(1));
            Assert.That(plan.BoardsNeededFor(Joists), Is.EqualTo(2), "1800 + 1800 don't fit one 2400 board.");
            Assert.That(plan.BoardsNeededFor(Decking), Is.EqualTo(1));
            Assert.That(plan.NeedsRipping, Is.False, "The 145 mm beam stock is not ripped for 120 mm joists: they have their own stock.");
        }

        [Test]
        public void ShoppingList_HasALinePerStock()
        {
            MaterialPlan plan = MaterialPlan.Calculate(MiniTerrace(), new[] { Decking, Beams, Joists, Blocks }, Mm(2));
            var catalog = new MaterialCatalog(new[]
            {
                CatalogItem.Board("deck", Decking, Money.FromEuros(12m)), CatalogItem.Board("beam", Beams, Money.FromEuros(14m)),
                CatalogItem.Board("joist", Joists, Money.FromEuros(11m)), CatalogItem.Board("block", Blocks, Money.FromEuros(4.5m))
            });

            ShoppingList list = ShoppingList.For(plan, catalog, stock => stock == Blocks ? 1 : 0, null);

            Assert.That(list.Lines.Count, Is.EqualTo(4 + 1)); // four stocks + the brackets (not sold here)
            Assert.That(list.TotalCost, Is.EqualTo(Money.FromEuros(12m + 14m + 22m + 4.5m)));
        }

        [Test]
        public void Concrete_DoesNotCountForSplittingOrTorque_ConnectorsNeverSplit()
        {
            var build = new AssemblyBuild(MiniTerrace());
            build.TryPlace("pier-1", new TimberPiece(Concrete, Dim(200, 200, 200)));
            build.TryPlace("pier-2", new TimberPiece(Concrete, Dim(200, 200, 200)));
            build.TryPlace("beam", new TimberPiece(Spruce, Dim(2400, 145, 45)));

            Assert.That(build.SeatingTorque("pier-1-beam", Bracket), Is.EqualTo(FasteningRules.SeatingTorque(Bracket, 450)));
            Assert.That(build.NeedsPilotHole("pier-1-beam", 0, Bracket), Is.False);
            Assert.That(build.TryFasten("pier-1-beam", 0, Bracket), Is.EqualTo(FastenResult.Fastened));
            Assert.That(build.IsSplit("pier-1-beam", 0), Is.False);
        }

        [Test]
        public void Deposit_IsAShareOfThePay()
        {
            var job = new Job("terrace", "Terrace", "", MiniTerrace(), Decking, pay: Money.FromEuros(650m),
                extraStocks: new[] { Beams, Joists, Blocks }, depositShare: 0.5);

            Assert.That(job.Deposit, Is.EqualTo(Money.FromEuros(325m)));
            Assert.That(job.Stocks.Count, Is.EqualTo(4));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new Job("x", "X", "", MiniTerrace(), Decking, depositShare: 0.9));
        }
    }
}
