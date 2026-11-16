using NUnit.Framework;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Construction
{
    /// <summary>Plumbing and bracing posts, working height (Phase 11, D-049).</summary>
    public sealed class PlumbTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly WoodSpecies Concrete = new WoodSpecies("concrete", "Concrete", 2200, isTimber: false);
        private static readonly FastenerSpec Anchor = new FastenerSpec("post-anchor", "Post base", Length.FromMillimeters(5),
            Length.FromMillimeters(40), FastenerKind.Connector);

        private static BoardDimensions Dim(double length, double width, double thickness) =>
            BoardDimensions.FromMillimeters(length, width, thickness);

        private static Blueprint PostOnPier()
        {
            var slots = new[]
            {
                new PartSlot("pier", "Pier", Dim(200, 200, 200)),
                new PartSlot("post", "Post", Dim(2200, 100, 100), new[] { "pier" }, requiresPlumb: true),
                new PartSlot("beam", "Beam", Dim(3000, 145, 45), new[] { "post" })
            };
            var connections = new[]
            {
                new ConnectionSpec("post-base", "post", "pier", Anchor.Id, 1),
                new ConnectionSpec("post-beam", "beam", "post", Anchor.Id, 1)
            };
            return new Blueprint("p", "Pergola corner", slots, connections, new Tolerance(Length.FromMillimeters(2)),
                new Tolerance(Length.FromMillimeters(8)));
        }

        /// <summary>A build whose fresh posts lean by the given random numbers (x first, then z).</summary>
        private static AssemblyBuild Build(params double[] randoms)
        {
            int next = 0;
            var build = new AssemblyBuild(PostOnPier(), () => randoms[next++ % randoms.Length]);
            build.TryPlace("pier", new TimberPiece(Concrete, Dim(200, 200, 200)));
            return build;
        }

        private static void SetPost(AssemblyBuild build) => build.TryPlace("post", new TimberPiece(Spruce, Dim(2200, 100, 100)));

        [TestCase(0.0, -10)]
        [TestCase(0.4999, -30)]
        [TestCase(0.5, 10)]
        [TestCase(0.75, 20)]
        [TestCase(0.9999, 30)]
        public void InitialLean_IsOneToThreeDegreesEitherWay(double random, int expected)
        {
            Assert.That(PlumbRules.InitialLean(random), Is.EqualTo(expected));
        }

        [Test]
        public void SetPost_LeansAndIsNotBraced_OtherPartsDont()
        {
            AssemblyBuild build = Build(0.25, 0.9);
            SetPost(build);

            Assert.That(build.TryGetLean("post", out PostLean lean), Is.True);
            Assert.That(lean.XTenths, Is.EqualTo(-20));
            Assert.That(lean.ZTenths, Is.EqualTo(26));
            Assert.That(lean.Braced, Is.False);
            Assert.That(lean.IsPlumb, Is.False);
            Assert.That(build.TryGetLean("pier", out _), Is.False, "Only posts lean.");
        }

        [Test]
        public void Adjust_ChangesOneDirection_LimitedToFiveDegrees()
        {
            AssemblyBuild build = Build(0.25, 0.9);
            SetPost(build);

            Assert.That(build.TryAdjustLean("post", LeanAxis.X, 18), Is.EqualTo(PlumbResult.Done));
            build.TryGetLean("post", out PostLean lean);
            Assert.That(lean.XTenths, Is.EqualTo(-2));
            Assert.That(lean.IsPlumbAlong(LeanAxis.X), Is.True);
            Assert.That(lean.IsPlumbAlong(LeanAxis.Z), Is.False);

            build.TryAdjustLean("post", LeanAxis.Z, 500);
            build.TryGetLean("post", out lean);
            Assert.That(lean.ZTenths, Is.EqualTo(PlumbRules.MaxLeanTenths));
        }

        [Test]
        public void Brace_NeedsPlumbInBothDirections_ThenLocksThePost()
        {
            AssemblyBuild build = Build(0.25, 0.9);
            SetPost(build);
            build.TryAdjustLean("post", LeanAxis.X, 20);

            Assert.That(build.TryBrace("post"), Is.EqualTo(PlumbResult.NotPlumb), "Still 2.6° along Z.");

            build.TryAdjustLean("post", LeanAxis.Z, -22); // 0.4°: within the 0.5° tolerance
            Assert.That(build.TryBrace("post"), Is.EqualTo(PlumbResult.Done));
            Assert.That(build.TryAdjustLean("post", LeanAxis.X, 1), Is.EqualTo(PlumbResult.Braced));
            build.TryGetLean("post", out PostLean lean);
            Assert.That(lean.Braced, Is.True);
            Assert.That(lean.ZTenths, Is.EqualTo(4));
        }

        [Test]
        public void Fasten_RefusedUntilThePostIsBraced()
        {
            AssemblyBuild build = Build(0.25, 0.9);
            SetPost(build);

            Assert.That(build.UnbracedPost("post-base"), Is.EqualTo("post"));
            Assert.That(build.TryFasten("post-base", 0, Anchor), Is.EqualTo(FastenResult.NotBraced));

            build.TryAdjustLean("post", LeanAxis.X, 20);
            build.TryAdjustLean("post", LeanAxis.Z, -28);
            build.TryBrace("post");

            Assert.That(build.UnbracedPost("post-base"), Is.Null);
            Assert.That(build.TryFasten("post-base", 0, Anchor), Is.EqualTo(FastenResult.Fastened));
        }

        [Test]
        public void Beam_GoesOnlyOntoABracedPost()
        {
            AssemblyBuild build = Build(0.25, 0.9);
            SetPost(build);
            var beam = new TimberPiece(Spruce, Dim(3000, 145, 45));

            Assert.That(build.CanPlace("beam", beam), Is.EqualTo(PlaceResult.PrerequisiteNotBraced));

            build.TryAdjustLean("post", LeanAxis.X, 20);
            build.TryAdjustLean("post", LeanAxis.Z, -26);
            build.TryBrace("post");

            Assert.That(build.TryPlace("beam", beam), Is.EqualTo(PlaceResult.Placed));
        }

        [Test]
        public void TakingThePostOut_ForgetsItsLean_SettingItAgainLeansAnew()
        {
            AssemblyBuild build = Build(0.25, 0.9, 0.6, 0.1);
            SetPost(build);
            build.TryRemove("post", out _);

            Assert.That(build.TryGetLean("post", out _), Is.False);

            SetPost(build);
            build.TryGetLean("post", out PostLean lean);
            Assert.That(lean.XTenths, Is.EqualTo(14));
            Assert.That(lean.ZTenths, Is.EqualTo(-14));
        }

        [Test]
        public void NotAPost_IsRefused()
        {
            AssemblyBuild build = Build(0.5);
            Assert.That(build.TryAdjustLean("pier", LeanAxis.X, 1), Is.EqualTo(PlumbResult.NotAPost));
            Assert.That(build.TryBrace("post"), Is.EqualTo(PlumbResult.SlotEmpty));
            Assert.That(build.TryBrace("nothing"), Is.EqualTo(PlumbResult.UnknownSlot));
        }

        [Test]
        public void RestoreLean_SetsTheSavedState()
        {
            AssemblyBuild build = Build(0.25, 0.9);
            SetPost(build);
            build.RestoreLean("post", 3, -2, braced: true);

            build.TryGetLean("post", out PostLean lean);
            Assert.That((lean.XTenths, lean.ZTenths, lean.Braced), Is.EqualTo((3, -2, true)));
        }

        [Test]
        public void Bubble_ShowsMillimetersPerMeter()
        {
            Assert.That(PlumbRules.MillimetersPerMeter(10), Is.EqualTo(17.45).Within(0.01));
            Assert.That(PlumbRules.MillimetersPerMeter(-5), Is.EqualTo(-8.73).Within(0.01));
            Assert.That(PlumbRules.Format(-14), Is.EqualTo("1.4°"));
        }

        [Test]
        public void PergolaPlan_PostsFromTheirOwnStock_CutTo2200()
        {
            var slots = new System.Collections.Generic.List<PartSlot>();
            for (int i = 1; i <= 4; i++)
            {
                slots.Add(new PartSlot($"pier-{i}", "Pier", Dim(200, 200, 200)));
                slots.Add(new PartSlot($"post-{i}", "Post", Dim(2200, 100, 100), new[] { $"pier-{i}" }, requiresPlumb: true));
            }

            slots.Add(new PartSlot("beam-front", "Beam", Dim(3000, 145, 45)));
            slots.Add(new PartSlot("beam-back", "Beam", Dim(3000, 145, 45)));
            for (int r = 1; r <= 5; r++)
            {
                slots.Add(new PartSlot($"rafter-{r}", "Rafter", Dim(3000, 120, 45)));
            }

            var pergola = new Blueprint("pergola", "Pergola", slots, new ConnectionSpec[0],
                new Tolerance(Length.FromMillimeters(2)), new Tolerance(Length.FromMillimeters(8)));
            var rafters = new StockSpec("spruce", "Spruce", Dim(3000, 120, 45));
            var beams = new StockSpec("spruce", "Spruce", Dim(3000, 145, 45));
            var posts = new StockSpec("spruce", "Spruce", Dim(2400, 100, 100));
            var blocks = new StockSpec("concrete", "Concrete", Dim(200, 200, 200));

            WoodcraftSimulator.Jobs.MaterialPlan plan = WoodcraftSimulator.Jobs.MaterialPlan.Calculate(pergola, new[] { rafters, beams, posts, blocks },
                Length.FromMillimeters(2));

            Assert.That(plan.IsFeasible, Is.True, string.Join(" ", plan.Problems));
            Assert.That(plan.BoardsNeededFor(posts), Is.EqualTo(4));
            Assert.That(plan.BoardsNeededFor(beams), Is.EqualTo(2));
            Assert.That(plan.BoardsNeededFor(rafters), Is.EqualTo(5));
            Assert.That(plan.BoardsNeededFor(blocks), Is.EqualTo(4));
            Assert.That(plan.NeedsRipping, Is.False);
        }

        [Test]
        public void WorkReach_AboutTwoPointOneMetersAboveTheFeet()
        {
            Assert.That(WorkReach.CanReach(Length.FromMillimeters(2100)), Is.True);
            Assert.That(WorkReach.CanReach(Length.FromMillimeters(2450)), Is.False, "A beam on 2.4 m posts needs a ladder.");
        }
    }
}
