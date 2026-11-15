using NUnit.Framework;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Cutting
{
    /// <summary>Rip cuts and plans with ripped parts (D-045).</summary>
    public sealed class RipcutTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly Saw HandSaw = new Saw("saw", "Saw", Length.FromMillimeters(2));

        private static Length Mm(double value) => Length.FromMillimeters(value);

        private static BoardDimensions Size(double length, double width) => BoardDimensions.FromMillimeters(length, width, 18);

        [Test]
        public void Rip_StripKeepsTheWidth_RestLosesTheKerf_MarksStayOnBoth()
        {
            var piece = new TimberPiece(Spruce, Size(2000, 145));
            var marks = new BoardMarks(Mm(2000));
            marks.Add(new PencilMark(Mm(436), BoardEnd.Start, BoardFace.Top));

            Assert.That(Ripcut.TryRip(piece, marks, Mm(100), HandSaw, out RipResult result), Is.EqualTo(RipRefusal.None));
            Assert.That(result.Strip.Dimensions, Is.EqualTo(Size(2000, 100)));
            Assert.That(result.Rest.Dimensions, Is.EqualTo(Size(2000, 43)));
            Assert.That(result.StripMarks.Count, Is.EqualTo(1));
            Assert.That(result.RestMarks.Count, Is.EqualTo(1));
            Assert.That(result.Waste.VolumeCubicMeters, Is.EqualTo(0.002 * 2.0 * 0.018).Within(1e-12));
        }

        [TestCase(5)]
        [TestCase(140)]
        public void Rip_TooNarrowStripOrRest_IsRefused(double width)
        {
            var piece = new TimberPiece(Spruce, Size(1000, 145));
            Assert.That(Ripcut.TryRip(piece, new BoardMarks(Mm(1000)), Mm(width), HandSaw, out _), Is.EqualTo(RipRefusal.TooNarrow));
        }

        [Test]
        public void Plan_NarrowParts_AreRippedFromStrips()
        {
            var slots = new[]
            {
                new PartSlot("base", "Base", Size(400, 145)),
                new PartSlot("end-l", "End", Size(250, 145)),
                new PartSlot("end-r", "End", Size(250, 145)),
                new PartSlot("side-f", "Side", Size(436, 100)),
                new PartSlot("side-b", "Side", Size(436, 100)),
                new PartSlot("handle", "Handle", Size(400, 45))
            };
            var blueprint = new Blueprint("tote", "Tote", slots, new ConnectionSpec[0],
                new Tolerance(Mm(1)), new Tolerance(Mm(5)));

            MaterialPlan plan = MaterialPlan.Calculate(blueprint, new StockSpec("spruce", "Spruce", Size(2000, 145)), Mm(2));

            Assert.That(plan.IsFeasible, Is.True);
            Assert.That(plan.NeedsRipping, Is.True);
            // One board across (400 + 250 + 250), one ripped to a 100 mm strip (2 × 436), one to a 45 mm strip (400).
            Assert.That(plan.BoardsNeeded, Is.EqualTo(3));
            Assert.That(plan.Boards[1].RipWidth, Is.EqualTo(Mm(45)));
            Assert.That(plan.Boards[2].RipWidth, Is.EqualTo(Mm(100)));
            Assert.That(plan.Boards[2].Parts, Is.EqualTo(new[] { Mm(436), Mm(436) }));

            string offer = JobText.Offer(new Job("tote", "Tote", "", blueprint, plan.Stock), plan, null);
            StringAssert.Contains("rip 1 x 100 mm strip(s), then 436 + 436 mm", offer);
        }

        [Test]
        public void Plan_ManyNarrowStrips_ShareOneBoard()
        {
            var slots = new[]
            {
                new PartSlot("a", "A", Size(1900, 45)), new PartSlot("b", "B", Size(1900, 45)),
                new PartSlot("c", "C", Size(1900, 45)), new PartSlot("d", "D", Size(1900, 45))
            };
            var blueprint = new Blueprint("s", "S", slots, new ConnectionSpec[0], new Tolerance(Mm(1)), new Tolerance(Mm(5)));

            MaterialPlan plan = MaterialPlan.Calculate(blueprint, new StockSpec("spruce", "Spruce", Size(2000, 145)), Mm(2));

            // (145 + 2) / (45 + 2) = 3 strips per board: 4 strips need 2 boards.
            Assert.That(plan.BoardsNeeded, Is.EqualTo(2));
            Assert.That(plan.Boards[0].Strips, Is.EqualTo(3));
            Assert.That(plan.Boards[1].Strips, Is.EqualTo(1));
        }
    }
}
