using NUnit.Framework;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Construction
{
    /// <summary>Nails and glued dowels (D-045).</summary>
    public sealed class JoineryTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);

        private static readonly FastenerSpec Nail = new FastenerSpec("nail", "Nail", Length.FromMillimeters(2.8),
            Length.FromMillimeters(50), FastenerKind.Nail);

        private static readonly FastenerSpec Dowel = new FastenerSpec("dowel", "Dowel", Length.FromMillimeters(8),
            Length.FromMillimeters(40), FastenerKind.Dowel);

        private static BoardDimensions Size(double length) => BoardDimensions.FromMillimeters(length, 145, 18);

        private static AssemblyBuild TwoParts(FastenerSpec fastener, int points, double endDistanceMm = 100)
        {
            var endDistances = new Length[points];
            for (int i = 0; i < points; i++)
            {
                endDistances[i] = Length.FromMillimeters(endDistanceMm);
            }

            var blueprint = new Blueprint("b", "B",
                new[] { new PartSlot("a", "A", Size(300)), new PartSlot("b", "B", Size(300)) },
                new[] { new ConnectionSpec("a-b", "a", "b", fastener.Id, points, endDistances) },
                new Tolerance(Length.FromMillimeters(1)), new Tolerance(Length.FromMillimeters(5)));
            var build = new AssemblyBuild(blueprint);
            build.TryPlace("a", new TimberPiece(Spruce, Size(300)));
            build.TryPlace("b", new TimberPiece(Spruce, Size(300)));
            return build;
        }

        [Test]
        public void Nail_NearTheEnd_SplitsWithoutPilotHole_LikeAScrew()
        {
            AssemblyBuild build = TwoParts(Nail, 2, endDistanceMm: 9);
            build.TryDrillPilot("a-b", 1);

            Assert.That(build.TryFasten("a-b", 0, Nail), Is.EqualTo(FastenResult.Fastened));
            Assert.That(build.TryFasten("a-b", 1, Nail), Is.EqualTo(FastenResult.Fastened));
            Assert.That(build.IsSplit("a-b", 0), Is.True);
            Assert.That(build.IsSplit("a-b", 1), Is.False);
            Assert.That(build.IsComplete, Is.True);
        }

        [Test]
        public void Dowel_NeedsADrilledHole_AndNeverSplits()
        {
            AssemblyBuild build = TwoParts(Dowel, 1, endDistanceMm: 9);

            Assert.That(build.TryFasten("a-b", 0, Dowel), Is.EqualTo(FastenResult.HoleRequired));
            build.TryDrillPilot("a-b", 0);
            Assert.That(build.TryFasten("a-b", 0, Dowel), Is.EqualTo(FastenResult.Fastened));
            Assert.That(build.IsSplit("a-b", 0), Is.False);
        }

        [Test]
        public void GluedJoint_IsCompleteOnlyWhenDry()
        {
            AssemblyBuild build = TwoParts(Dowel, 2);
            int completed = 0;
            build.Completed += () => completed++;
            build.TryDrillPilot("a-b", 0);
            build.TryDrillPilot("a-b", 1);
            build.TryFasten("a-b", 0, Dowel);
            Assert.That(build.IsDrying("a-b"), Is.False, "Clamped only when the last dowel is in.");

            build.TryFasten("a-b", 1, Dowel);
            Assert.That(build.DryingSeconds("a-b"), Is.EqualTo(GlueRules.DryingSeconds));
            Assert.That(build.IsComplete, Is.False);
            Assert.That(build.CreateReport().DryingJoints, Is.EqualTo(1));

            build.AdvanceTime(GlueRules.DryingSeconds - 1.0);
            Assert.That(build.IsComplete, Is.False);
            build.AdvanceTime(2.0);
            Assert.That(build.IsComplete, Is.True);
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void Drying_IsRestoredFromASave()
        {
            AssemblyBuild build = TwoParts(Dowel, 1);
            build.RestorePilot("a-b", 0);
            build.RestoreFastened("a-b", 0, Length.Zero, Dowel);
            Assert.That(build.IsComplete, Is.True, "Restoring a dowel does not start drying by itself.");

            build.RestoreDrying("a-b", 10.0);
            Assert.That(build.IsComplete, Is.False);
            build.AdvanceTime(10.5);
            Assert.That(build.IsComplete, Is.True);
        }
    }
}
