using NUnit.Framework;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Construction
{
    /// <summary>Stage inspections of outdoor builds (Phase 11e, D-053).</summary>
    public sealed class StageInspectionTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly WoodSpecies Concrete = new WoodSpecies("concrete", "Concrete", 2200, isTimber: false);
        private static readonly FastenerSpec Screw = new FastenerSpec("screw", "Screw", Length.FromMillimeters(5),
            Length.FromMillimeters(60), FastenerKind.Screw);

        private static BoardDimensions Dim(double length, double width, double thickness) =>
            BoardDimensions.FromMillimeters(length, width, thickness);

        // Stages: 0 piers, 1 posts, 2 beams.
        private static int StageOf(string slotId) => slotId.StartsWith("pier") ? 0 : slotId.StartsWith("post") ? 1 : slotId.StartsWith("beam") ? 2 : -1;

        private static AssemblyBuild Build()
        {
            var slots = new[]
            {
                new PartSlot("pier", "Pier", Dim(200, 200, 200)),
                new PartSlot("post", "Post", Dim(2200, 100, 100), new[] { "pier" }),
                new PartSlot("beam", "Beam", Dim(3000, 145, 45), new[] { "post" })
            };
            var connections = new[]
            {
                new ConnectionSpec("post-base", "post", "pier", Screw.Id, 2),
                new ConnectionSpec("post-beam", "beam", "post", Screw.Id, 1)
            };
            var build = new AssemblyBuild(new Blueprint("p", "Corner", slots, connections,
                new Tolerance(Length.FromMillimeters(2)), new Tolerance(Length.FromMillimeters(8))));
            build.TryPlace("pier", new TimberPiece(Concrete, Dim(200, 200, 200)));
            return build;
        }

        [Test]
        public void ConnectionBelongsToTheLaterPart_PiersHaveNothingToCheck()
        {
            AssemblyBuild build = Build();

            Assert.That(StageInspection.HasChecks(build.Blueprint, 0, StageOf), Is.False, "No joint belongs to the piers.");
            Assert.That(StageInspection.HasChecks(build.Blueprint, 1, StageOf), Is.True, "Post base.");
            Assert.That(StageInspection.HasChecks(build.Blueprint, 2, StageOf), Is.True, "Beam on post.");
        }

        [Test]
        public void LooseJointAndProudHead_AreProblems_UntilFixed()
        {
            AssemblyBuild build = Build();
            build.TryPlace("post", new TimberPiece(Spruce, Dim(2200, 100, 100)));
            Assert.That(StageInspection.AllPartsSet(build, 1, StageOf), Is.True);

            build.TryFasten("post-base", 0, Screw, Length.FromMillimeters(2)); // head 2 mm proud
            InspectionResult result = StageInspection.Inspect(build, 1, StageOf);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Problems, Has.Count.EqualTo(2));
            StringAssert.Contains("1/2 not fastened", result.Problems[0]);
            StringAssert.Contains("1 screw head(s) stand out", result.Problems[1]);

            build.TryDriveFurther("post-base", 0, Length.Zero);
            build.TryFasten("post-base", 1, Screw);
            Assert.That(StageInspection.Inspect(build, 1, StageOf).Passed, Is.True);
        }

        [Test]
        public void InaccuratePartAndDeepScrew_AreNotes_StagePasses()
        {
            AssemblyBuild build = Build();
            build.TryPlace("post", new TimberPiece(Spruce, Dim(2205, 100, 100))); // fits (8 mm), not accurate (2 mm)
            build.TryFasten("post-base", 0, Screw, Length.FromMillimeters(-3));
            build.TryFasten("post-base", 1, Screw);

            InspectionResult result = StageInspection.Inspect(build, 1, StageOf);

            Assert.That(result.Passed, Is.True);
            Assert.That(result.Notes, Has.Count.EqualTo(2));
        }

        [Test]
        public void Log_FirstInspectionDecides_FirstTime()
        {
            var log = new InspectionLog();
            var fail = new InspectionResult(new[] { "loose" }, null);
            var pass = new InspectionResult(null, null);

            log.Record("post", fail);
            log.Record("post", pass);
            log.Record("beam", pass);

            Assert.That(log.IsPassed("post"), Is.True);
            Assert.That(log.Inspected, Is.EqualTo(2));
            Assert.That(log.PassedFirstTime, Is.EqualTo(1));

            var restored = new InspectionLog();
            restored.Restore(log.Passed, log.FailedFirst);
            Assert.That(restored.PassedFirstTime, Is.EqualTo(1));
            Assert.That(restored.WasRequested("post"), Is.True);
        }
    }
}
