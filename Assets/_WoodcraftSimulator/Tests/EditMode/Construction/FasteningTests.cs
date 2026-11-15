using System;
using NUnit.Framework;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Construction
{
    public sealed class FasteningTests
    {
        private static readonly FastenerSpec Screw4x40 = new FastenerSpec("wood-screw-4x40", "Wood screw 4 × 40 mm",
            Length.FromMillimeters(4), Length.FromMillimeters(40));
        private static readonly FastenerSpec Screw8x100 = new FastenerSpec("wood-screw-8x100", "Wood screw 8 × 100 mm",
            Length.FromMillimeters(8), Length.FromMillimeters(100));

        private static Length Mm(double value) => Length.FromMillimeters(value);

        [TestCase(9.0, 450.0, true)]      // near the end of a spruce board
        [TestCase(39.9, 450.0, true)]     // just under 10 diameters
        [TestCase(40.0, 450.0, false)]
        [TestCase(300.0, 450.0, false)]
        [TestCase(300.0, 700.0, true)]    // dense wood (oak): always
        public void NeedsPilotHole_EndDistanceAndDensity(double endDistanceMm, double density, bool expected)
        {
            Assert.That(FasteningRules.NeedsPilotHole(Screw4x40, Mm(endDistanceMm), density), Is.EqualTo(expected));
        }

        [Test]
        public void NeedsPilotHole_ThickScrew_AndUnknownEndDistance()
        {
            Assert.That(FasteningRules.NeedsPilotHole(Screw8x100, null, 450.0), Is.True);
            Assert.That(FasteningRules.NeedsPilotHole(Screw4x40, null, 450.0), Is.False);
        }

        [TestCase(0.6, ScrewSeat.Proud)]
        [TestCase(0.5, ScrewSeat.Flush)]
        [TestCase(0.0, ScrewSeat.Flush)]
        [TestCase(-1.0, ScrewSeat.Flush)]
        [TestCase(-1.1, ScrewSeat.Overdriven)]
        public void Seat_FromHeadHeight(double headHeightMm, ScrewSeat expected)
        {
            Assert.That(FasteningRules.Seat(Mm(headHeightMm)), Is.EqualTo(expected));
        }

        [Test]
        public void SeatingTorque_GrowsWithDensityAndSize()
        {
            double spruce = FasteningRules.SeatingTorque(Screw4x40, 450.0);
            Assert.That(spruce, Is.EqualTo(8.0).Within(1e-9));
            Assert.That(FasteningRules.SeatingTorque(Screw4x40, 700.0), Is.GreaterThan(spruce));
            Assert.That(FasteningRules.SeatingTorque(Screw8x100, 450.0), Is.GreaterThan(spruce));
        }

        private static ScrewDrive Drive(double seconds, int clutch)
        {
            var drive = new ScrewDrive(Screw4x40, seatingTorque: 8.0);
            for (double t = 0.0; t < seconds; t += 0.02)
            {
                drive.Advance(0.02, clutch);
            }

            return drive;
        }

        [TestCase(8.0, 5, 7)]         // spruce, 4 × 40 mm
        [TestCase(10.0, 6, 9)]        // exact 60 % still stops flush; 10 itself sinks
        [TestCase(14.142, 9, 14)]     // spruce, 5 × 80 mm
        public void FlushClutchRange_MatchesTheDrive(double torque, int lowest, int highest)
        {
            Assert.That(ScrewDrive.TryFlushClutchRange(torque, out int low, out int high), Is.True);
            Assert.That(low, Is.EqualTo(lowest));
            Assert.That(high, Is.EqualTo(highest));

            // The advice must agree with what the drive really does when the trigger is held long.
            for (int clutch = ScrewDrive.MinClutch; clutch <= ScrewDrive.MaxClutch; clutch++)
            {
                var drive = new ScrewDrive(Screw4x40, torque);
                for (int i = 0; i < 300; i++)
                {
                    drive.Advance(0.02, clutch);
                }

                ScrewSeat expected = clutch < low ? ScrewSeat.Proud : clutch <= high ? ScrewSeat.Flush : ScrewSeat.Overdriven;
                Assert.That(drive.Seat, Is.EqualTo(expected), $"clutch {clutch}");
            }
        }

        [Test]
        public void FlushClutchRange_TooStiffForTheScale_IsNone()
        {
            Assert.That(ScrewDrive.TryFlushClutchRange(60.0, out _, out _), Is.False);
        }

        [Test]
        public void RightClutch_StopsFlush_EvenWhenHeldLong()
        {
            ScrewDrive drive = Drive(5.0, clutch: 6);

            Assert.That(drive.HeadHeight, Is.EqualTo(Length.Zero));
            Assert.That(drive.Seat, Is.EqualTo(ScrewSeat.Flush));
            Assert.That(drive.IsSlipping, Is.True);
        }

        [Test]
        public void LowClutch_StopsPartWay_Proud()
        {
            ScrewDrive drive = Drive(5.0, clutch: 3);

            // 3 / (0.6 × 8) of 40 mm = 25 mm in.
            Assert.That(drive.Depth.Millimeters, Is.EqualTo(25.0).Within(0.1));
            Assert.That(drive.Seat, Is.EqualTo(ScrewSeat.Proud));
        }

        [Test]
        public void HighClutch_HeldTooLong_Overdriven_UpToTheLimit()
        {
            Assert.That(Drive(5.0, clutch: 8).Seat, Is.EqualTo(ScrewSeat.Overdriven));

            ScrewDrive drillMode = Drive(10.0, ScrewDrive.DrillMode);
            Assert.That(drillMode.HeadHeight, Is.EqualTo(-ScrewDrive.MaximumSink));
        }

        [Test]
        public void HighClutch_ReleasedRightAtTheSurface_IsFlush()
        {
            // 40 mm at 50 mm/s: flush after 0.8 s.
            var drive = new ScrewDrive(Screw4x40, 8.0);
            drive.Advance(0.8, ScrewDrive.DrillMode);

            Assert.That(drive.Seat, Is.EqualTo(ScrewSeat.Flush));
        }

        [Test]
        public void ReleasedEarly_Proud_CanContinueFromThere()
        {
            var drive = new ScrewDrive(Screw4x40, 8.0);
            drive.Advance(0.4, 6);
            Assert.That(drive.HeadHeight, Is.EqualTo(Mm(20)));

            var further = new ScrewDrive(Screw4x40, 8.0, startDepth: drive.Depth);
            further.Advance(1.0, 6);
            Assert.That(further.Seat, Is.EqualTo(ScrewSeat.Flush));
        }

        [Test]
        public void Advance_InvalidClutch_Throws()
        {
            var drive = new ScrewDrive(Screw4x40, 8.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => drive.Advance(0.1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => drive.Advance(0.1, ScrewDrive.DrillMode + 1));
        }

        // --- in an assembly ---

        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly WoodSpecies Oak = new WoodSpecies("oak", "Oak", 700);

        /// <summary>One side and one shelf; point 0 is 9 mm from the side's end, point 1 is 300 mm away.</summary>
        private static AssemblyBuild SideAndShelf(WoodSpecies species)
        {
            var blueprint = new Blueprint("test", "Test",
                new[]
                {
                    new PartSlot("side", "Side", BoardDimensions.FromMillimeters(600, 145, 18)),
                    new PartSlot("shelf", "Shelf", BoardDimensions.FromMillimeters(464, 145, 18))
                },
                new[] { new ConnectionSpec("joint", "side", "shelf", Screw4x40.Id, 2, new[] { Mm(9), Mm(300) }) },
                new Tolerance(Mm(1)), new Tolerance(Mm(5)));
            var build = new AssemblyBuild(blueprint);
            build.TryPlace("side", new TimberPiece(species, BoardDimensions.FromMillimeters(600, 145, 18)));
            build.TryPlace("shelf", new TimberPiece(species, BoardDimensions.FromMillimeters(464, 145, 18)));
            return build;
        }

        [Test]
        public void ScrewNearTheEnd_WithoutPilotHole_Splits_WithPilotHole_DoesNot()
        {
            AssemblyBuild build = SideAndShelf(Spruce);
            Assert.That(build.NeedsPilotHole("joint", 0, Screw4x40), Is.True);
            Assert.That(build.NeedsPilotHole("joint", 1, Screw4x40), Is.False);

            build.TryFasten("joint", 0, Screw4x40);
            build.TryFasten("joint", 1, Screw4x40);
            Assert.That(build.IsSplit("joint", 0), Is.True);
            Assert.That(build.IsSplit("joint", 1), Is.False);
            Assert.That(build.CreateReport().Splits, Is.EqualTo(1));

            AssemblyBuild careful = SideAndShelf(Spruce);
            Assert.That(careful.TryDrillPilot("joint", 0), Is.EqualTo(PilotResult.Drilled));
            careful.TryFasten("joint", 0, Screw4x40);
            Assert.That(careful.IsSplit("joint", 0), Is.False);
        }

        [Test]
        public void DenseWood_SplitsAnywhereWithoutPilotHole()
        {
            AssemblyBuild build = SideAndShelf(Oak);
            build.TryFasten("joint", 1, Screw4x40);

            Assert.That(build.IsSplit("joint", 1), Is.True);
        }

        [Test]
        public void TryDrillPilot_Refusals()
        {
            var empty = new AssemblyBuild(SideAndShelf(Spruce).Blueprint);
            Assert.That(empty.TryDrillPilot("joint", 0), Is.EqualTo(PilotResult.PartsMissing));

            AssemblyBuild build = SideAndShelf(Spruce);
            build.TryDrillPilot("joint", 0);
            Assert.That(build.TryDrillPilot("joint", 0), Is.EqualTo(PilotResult.AlreadyDrilled));
            Assert.That(build.TryDrillPilot("joint", 2), Is.EqualTo(PilotResult.InvalidPoint));
            Assert.That(build.TryDrillPilot("other", 0), Is.EqualTo(PilotResult.UnknownConnection));
            build.TryFasten("joint", 1, Screw4x40);
            Assert.That(build.TryDrillPilot("joint", 1), Is.EqualTo(PilotResult.AlreadyFastened));
        }

        [Test]
        public void PilotHole_LocksThePartsLikeAScrew()
        {
            AssemblyBuild build = SideAndShelf(Spruce);
            build.TryDrillPilot("joint", 0);

            Assert.That(build.CanRemove("shelf"), Is.EqualTo(RemoveResult.Fastened));
        }

        [Test]
        public void HeadHeights_AreReported_ProudCanBeDrivenFurther()
        {
            AssemblyBuild build = SideAndShelf(Spruce);
            build.TryDrillPilot("joint", 0);
            build.TryFasten("joint", 0, Screw4x40, Mm(3));
            build.TryFasten("joint", 1, Screw4x40, Mm(-2));

            AssemblyQualityReport report = build.CreateReport();
            Assert.That(report.ProudFasteners, Is.EqualTo(1));
            Assert.That(report.OverdrivenFasteners, Is.EqualTo(1));
            Assert.That(report.IsComplete, Is.True); // complete, quality reported separately

            Assert.That(build.TryDriveFurther("joint", 1, Mm(-3)), Is.EqualTo(FastenResult.NotProud));
            Assert.That(build.TryDriveFurther("joint", 0, Mm(4)), Is.EqualTo(FastenResult.NotProud));
            Assert.That(build.TryDriveFurther("joint", 0, Length.Zero), Is.EqualTo(FastenResult.Fastened));
            Assert.That(build.CreateReport().FlushFasteners, Is.EqualTo(1));
        }

        [Test]
        public void RestoreFastened_RecomputesTheSplitFromTheRules()
        {
            AssemblyBuild build = SideAndShelf(Spruce);
            build.RestoreFastened("joint", 0, Mm(0.2), Screw4x40);

            Assert.That(build.IsSplit("joint", 0), Is.True);
            Assert.That(build.HeadHeight("joint", 0), Is.EqualTo(Mm(0.2)));
            Assert.That(build.HeadHeight("joint", 1), Is.Null);
        }

        [Test]
        public void EndDistances_MustMatchTheFastenerCount()
        {
            Assert.Throws<ArgumentException>(() => new ConnectionSpec("j", "a", "b", "s", 2, new[] { Mm(9) }));
        }
    }
}
