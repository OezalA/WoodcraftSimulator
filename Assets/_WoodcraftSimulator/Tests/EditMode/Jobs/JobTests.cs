using System.Linq;
using NUnit.Framework;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Jobs
{
    public sealed class JobTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly FastenerSpec Screw = new FastenerSpec("wood-screw-4x40", "Wood screw 4 × 40 mm",
            Length.FromMillimeters(4), Length.FromMillimeters(40));
        private static readonly Length Kerf = Length.FromMillimeters(2);

        private static Length Mm(double value) => Length.FromMillimeters(value);

        private static BoardDimensions Size(double length) => BoardDimensions.FromMillimeters(length, 145, 18);

        /// <summary>The Phase 6 shelf: 2 sides 600 mm, 3 shelves 464 mm, 18 × 145 mm boards, 2 screws per joint.</summary>
        private static Blueprint Shelf()
        {
            var slots = new[]
            {
                new PartSlot("left", "Left side", Size(600)),
                new PartSlot("right", "Right side", Size(600)),
                new PartSlot("bottom", "Bottom shelf", Size(464)),
                new PartSlot("middle", "Middle shelf", Size(464)),
                new PartSlot("top", "Top shelf", Size(464))
            };
            var connections = new[] { "left", "right" }
                .SelectMany(side => new[] { "bottom", "middle", "top" }
                    .Select(shelf => new ConnectionSpec($"{side}-{shelf}", side, shelf, Screw.Id, 2)))
                .ToArray();
            return new Blueprint("wall-shelf", "Wall shelf", slots, connections, new Tolerance(Mm(1)), new Tolerance(Mm(5)));
        }

        private static Job ShelfJob(int spare = 0) => new Job("shelf-job", "Wall shelf", "A small shelf.", Shelf(),
            new StockSpec("spruce", "Spruce", Size(2000)), spare, new[] { "Pre-drill near board ends." });

        [Test]
        public void Plan_GroupsTheCutList_AndPacksTheStock()
        {
            MaterialPlan plan = ShelfJob().Plan(Kerf);

            Assert.That(plan.IsFeasible, Is.True);
            Assert.That(plan.Lines.Count, Is.EqualTo(2));
            Assert.That(plan.Lines[0].Quantity, Is.EqualTo(2));
            Assert.That(plan.Lines[0].Length, Is.EqualTo(Mm(600)));
            Assert.That(plan.Lines[1].Quantity, Is.EqualTo(3));

            // Board 1: 600 + 600 + 464 (+ 3 kerfs) → 330 left; board 2: 464 + 464 (+ 2 kerfs) → 1068 left.
            Assert.That(plan.BoardsNeeded, Is.EqualTo(2));
            Assert.That(plan.Boards[0].Parts, Is.EqualTo(new[] { Mm(600), Mm(600), Mm(464) }));
            Assert.That(plan.Boards[0].Offcut, Is.EqualTo(Mm(330)));
            Assert.That(plan.Boards[1].Offcut, Is.EqualTo(Mm(1068)));
            Assert.That(plan.Fasteners[Screw.Id], Is.EqualTo(12));
        }

        [Test]
        public void Plan_PartUsingTheWholeBoard_NeedsNoCut()
        {
            var blueprint = new Blueprint("b", "B", new[] { new PartSlot("a", "A", Size(2000)) },
                new ConnectionSpec[0], new Tolerance(Mm(1)), new Tolerance(Mm(1)));
            MaterialPlan plan = MaterialPlan.Calculate(blueprint, new StockSpec("spruce", "Spruce", Size(2000)), Kerf);

            Assert.That(plan.Boards[0].Cuts, Is.EqualTo(0));
            Assert.That(plan.Boards[0].Offcut, Is.EqualTo(Length.Zero));
        }

        [Test]
        public void Plan_WrongCrossSectionOrTooLong_IsNotFeasible()
        {
            var blueprint = new Blueprint("b", "B", new[]
                {
                    new PartSlot("a", "A", BoardDimensions.FromMillimeters(500, 95, 45)),
                    new PartSlot("b", "B", Size(2500))
                },
                new ConnectionSpec[0], new Tolerance(Mm(1)), new Tolerance(Mm(1)));
            MaterialPlan plan = MaterialPlan.Calculate(blueprint, new StockSpec("spruce", "Spruce", Size(2000)), Kerf);

            Assert.That(plan.IsFeasible, Is.False);
            Assert.That(plan.Problems.Count, Is.EqualTo(2));
        }

        [Test]
        public void BoardsToDeliver_IncludesSpares()
        {
            Assert.That(ShelfJob(spare: 1).BoardsToDeliver(Kerf), Is.EqualTo(3));
        }

        [Test]
        public void CutListProgress_CountsInstalledAndReadyPieces_EachPieceOnce()
        {
            Job job = ShelfJob();
            MaterialPlan plan = job.Plan(Kerf);
            var build = new AssemblyBuild(job.Blueprint);
            build.TryPlace("left", new TimberPiece(Spruce, Size(600)));

            var loose = new[]
            {
                new TimberPiece(Spruce, Size(600.4)),   // fits a side
                new TimberPiece(Spruce, Size(463)),     // fits a shelf
                new TimberPiece(Spruce, Size(470)),     // too long for anything
                new TimberPiece(Spruce, Size(1068))     // offcut
            };

            var progress = CutListProgress.Evaluate(plan, build, loose);

            Assert.That(progress[0].Installed, Is.EqualTo(1));
            Assert.That(progress[0].Ready, Is.EqualTo(1));
            Assert.That(progress[0].Missing, Is.EqualTo(0));
            Assert.That(progress[1].Ready, Is.EqualTo(1));
            Assert.That(progress[1].Missing, Is.EqualTo(2));
        }

        private static AssemblyBuild CompletedShelf(bool careful)
        {
            var build = new AssemblyBuild(Shelf());
            foreach (PartSlot slot in build.Blueprint.Slots)
            {
                build.TryPlace(slot.Id, new TimberPiece(Spruce, careful ? slot.Dimensions : Size(slot.Dimensions.Length.Millimeters + 2)));
            }

            foreach (ConnectionSpec connection in build.Blueprint.Connections)
            {
                for (int point = 0; point < connection.FastenerCount; point++)
                {
                    build.TryFasten(connection.Id, point, Screw, careful ? Length.Zero : Mm(2));
                }
            }

            return build;
        }

        [Test]
        public void Result_ListsGoalsSeparately()
        {
            Job job = ShelfJob();
            var usage = new MaterialUsage(2, 0, 2 * Size(2000).VolumeCubicMeters, 0.0072, 0.00002);

            JobResult good = JobResult.Create(job, CompletedShelf(careful: true).CreateReport(), usage, 754.0);
            Assert.That(good.GoalsMet, Is.EqualTo(good.Goals.Count));

            var extra = new MaterialUsage(3, 1, 3 * Size(2000).VolumeCubicMeters, 0.0072, 0.00002);
            JobResult rough = JobResult.Create(job, CompletedShelf(careful: false).CreateReport(), extra, 754.0);
            Assert.That(rough.Goals.Select(goal => goal.Met), Is.EqualTo(new[] { false, false, true, false }));

            string text = JobText.Result(rough, Screw.DisplayName);
            StringAssert.Contains("JOB COMPLETE: Wall shelf   (12:34)", text);
            StringAssert.Contains("[ ] Every screw head flush (0/12 flush)", text);
            StringAssert.Contains("[x] No split boards", text);
            StringAssert.Contains("1 ordered extra", text);
            StringAssert.EndsWith(AssemblyReportText.SafetyNote, text);
        }

        [Test]
        public void MaterialUsage_OffcutsAndUtilization()
        {
            var usage = new MaterialUsage(1, 0, 0.01, 0.006, 0.0001);

            Assert.That(usage.OffcutCubicMeters, Is.EqualTo(0.0039).Within(1e-12));
            Assert.That(usage.Utilization, Is.EqualTo(0.6).Within(1e-12));
            Assert.That(new MaterialUsage(0, 0, 0.0, 0.006, 0.0).OffcutCubicMeters, Is.EqualTo(0.0));
        }

        [Test]
        public void SheetAndOffer_ShowCutListAndMaterial()
        {
            Job job = ShelfJob();
            MaterialPlan plan = job.Plan(Kerf);
            var build = new AssemblyBuild(job.Blueprint);
            string sheet = JobText.Sheet(job, plan, CutListProgress.Evaluate(plan, build, null), id => "Wood screw 4 × 40 mm", 2, 65);

            StringAssert.Contains("JOB: Wall shelf   (1:05)", sheet);
            StringAssert.Contains("2 x 600 mm  Left side, Right side  - 0 in, 0 ready", sheet);
            StringAssert.Contains("Fasteners: 12 x Wood screw 4 × 40 mm", sheet);
            StringAssert.Contains("Tip: Pre-drill near board ends.", sheet);

            string offer = JobText.Offer(job, plan, null);
            StringAssert.Contains("Material: 2 x Spruce board 2000 × 145 × 18 mm", offer);
            StringAssert.Contains("Board 1: 600 + 600 + 464 mm, offcut 330 mm", offer);
        }

        private static MaterialCatalog Catalog() => new MaterialCatalog(new[]
        {
            CatalogItem.Board("spruce-2000x145x18", new StockSpec("spruce", "Spruce", Size(2000)), Money.FromEuros(8.50m)),
            CatalogItem.FastenerBox("screws-4x40-200", "Box of 200 screws", Screw.Id, 200, Money.FromEuros(6.00m))
        });

        private static Job PaidShelfJob() => new Job("shelf-job", "Wall shelf", "A small shelf.", Shelf(),
            new StockSpec("spruce", "Spruce", Size(2000)), pay: Money.FromEuros(60m));

        /// <summary>The shelf's parts (2 × 600 + 3 × 464 mm) and the sawdust of 5 cuts.</summary>
        private static MaterialUsage ShelfUsage(int boards, int extra) => new MaterialUsage(boards, extra,
            boards * Size(2000).VolumeCubicMeters, Size(2592).VolumeCubicMeters, 5 * Size(2).VolumeCubicMeters);

        [Test]
        public void ShoppingList_BuysOnlyWhatIsMissing()
        {
            Job job = PaidShelfJob();
            var screws = new FastenerStock();
            screws.Add(Screw.Id, 5);

            ShoppingList list = ShoppingList.For(job.Plan(Kerf), Catalog(), 1, screws, id => "Wood screw 4 × 40 mm");

            Assert.That(list.Lines[0].Needed, Is.EqualTo(2));
            Assert.That(list.Lines[0].UnitsToBuy, Is.EqualTo(1));
            Assert.That(list.Lines[1].Needed, Is.EqualTo(12));
            Assert.That(list.Lines[1].UnitsToBuy, Is.EqualTo(1));
            Assert.That(list.TotalCost, Is.EqualTo(Money.FromEuros(14.50m)));

            screws.Add(Screw.Id, 200);
            ShoppingList enough = ShoppingList.For(job.Plan(Kerf), Catalog(), 2, screws);
            Assert.That(enough.TotalCost, Is.EqualTo(Money.Zero));

            string text = JobText.Offer(job, job.Plan(Kerf), null, list);
            StringAssert.Contains("Wall shelf   Pay €60.00", text);
            StringAssert.Contains("2 x Spruce board 2000 × 145 × 18 mm: have 1, buy 1 x Spruce board 2000 × 145 × 18 mm = €8.50", text);
            StringAssert.Contains("12 x Wood screw 4 × 40 mm: have 5, buy 1 x Box of 200 screws = €6.00", text);
            StringAssert.Contains("Total to buy: €14.50", text);
        }

        [Test]
        public void ShoppingList_UnsoldMaterial_CostsNothing()
        {
            var catalog = new MaterialCatalog(new CatalogItem[0]);
            ShoppingList list = ShoppingList.For(PaidShelfJob().Plan(Kerf), catalog, 0, null);

            Assert.That(list.Lines[0].IsSold, Is.False);
            Assert.That(list.TotalCost, Is.EqualTo(Money.Zero));
            StringAssert.Contains("not sold by the merchant", JobText.Shopping(list));
        }

        [Test]
        public void Settlement_PaysForGoals_AndChargesTheMaterialUsed()
        {
            Job job = PaidShelfJob();
            JobResult good = JobResult.Create(job, CompletedShelf(careful: true).CreateReport(), ShelfUsage(2, 0), 600);

            JobSettlement settlement = JobSettlement.Calculate(good, Catalog());

            Assert.That(settlement.BasePay, Is.EqualTo(Money.FromEuros(42m)));
            Assert.That(settlement.Earned, Is.EqualTo(Money.FromEuros(60m)));

            // Used 2592 + 10 mm of the 2000 mm board: 1.301 × €8.50 = €11.06 (offcuts are not charged).
            Assert.That(settlement.TimberCost, Is.EqualTo(Money.FromEuros(11.06m)));
            Assert.That(settlement.FastenerCost, Is.EqualTo(Money.FromEuros(0.36m)));
            Assert.That(settlement.Profit, Is.EqualTo(Money.FromEuros(48.58m)));
            Assert.That(settlement.Unpriced, Is.Empty);

            string text = JobText.Result(good, Screw.DisplayName, settlement);
            StringAssert.Contains("Pay: €60.00 of €60.00 (delivery €42.00 + goals 4/4 €18.00)", text);
            StringAssert.Contains("Material used: timber €11.06, fasteners €0.36", text);
            StringAssert.Contains("Profit: €48.58", text);
        }

        [Test]
        public void Settlement_MissedGoals_LowerThePay()
        {
            Job job = PaidShelfJob();
            JobResult rough = JobResult.Create(job, CompletedShelf(careful: false).CreateReport(), ShelfUsage(3, 1), 600);

            JobSettlement settlement = JobSettlement.Calculate(rough, Catalog());

            // 1 of 4 goals (no splits): €42 + €18 / 4.
            Assert.That(settlement.Earned, Is.EqualTo(Money.FromEuros(46.50m)));
        }

        [Test]
        public void Settlement_WithoutPrices_ListsTheUnpricedMaterial()
        {
            JobResult good = JobResult.Create(PaidShelfJob(), CompletedShelf(careful: true).CreateReport(), ShelfUsage(2, 0), 600);

            JobSettlement settlement = JobSettlement.Calculate(good, new MaterialCatalog(new CatalogItem[0]));

            Assert.That(settlement.MaterialCost, Is.EqualTo(Money.Zero));
            Assert.That(settlement.Unpriced.Count, Is.EqualTo(2));
        }
    }
}
