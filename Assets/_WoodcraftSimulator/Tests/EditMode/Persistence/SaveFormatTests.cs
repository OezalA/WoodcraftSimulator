using System.Linq;
using NUnit.Framework;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;
using WoodcraftSimulator.Persistence;

namespace WoodcraftSimulator.Tests.Persistence
{
    public sealed class SaveFormatTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);

        private static Length Mm(double value) => Length.FromMillimeters(value);

        private static SaveGameData ValidSave()
        {
            return new SaveGameData
            {
                version = SaveFormat.CurrentVersion,
                boards = new[]
                {
                    new BoardData { id = "b1", speciesId = "spruce", lengthTenths = 6000, widthTenths = 950, thicknessTenths = 450 },
                    new BoardData { id = "b2", speciesId = "spruce", lengthTenths = 2100, widthTenths = 950, thicknessTenths = 450 }
                },
                items = new[]
                {
                    new ItemData { id = "TapeMeasure", onBelt = true },
                    new ItemData { id = "HandSaw", onBelt = false }
                },
                belt = new BeltData { slots = new[] { "TapeMeasure", "", "", "" }, selected = 0 },
                assemblies = new[]
                {
                    new AssemblyData
                    {
                        stationId = "AssemblyTable",
                        blueprintId = "frame-600x300",
                        parts = new[] { new InstalledPartData { slotId = "front", boardId = "b1" } },
                        fastened = new[] { new FastenedConnectionData { connectionId = "front-left", points = new[] { 0 } } }
                    }
                }
            };
        }

        [Test]
        public void ValidSave_HasNoErrors()
        {
            Assert.That(SaveFormat.Validate(ValidSave(), id => id == "spruce"), Is.Empty);
        }

        [Test]
        public void Migrate_CurrentVersion_IsAccepted()
        {
            Assert.That(SaveFormat.TryMigrate(ValidSave(), out string error), Is.True);
            Assert.That(error, Is.Null);
        }

        [TestCase(0)]
        [TestCase(SaveFormat.CurrentVersion + 1)]
        public void Migrate_UnknownOrNewerVersion_IsRefused(int version)
        {
            SaveGameData save = ValidSave();
            save.version = version;

            Assert.That(SaveFormat.TryMigrate(save, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void Migrate_Null_IsRefused()
        {
            Assert.That(SaveFormat.TryMigrate(null, out _), Is.False);
        }

        [Test]
        public void Migrate_Version1_GetsAnUnstartedEconomy()
        {
            SaveGameData save = ValidSave();
            save.version = 1;
            save.economy = null;

            Assert.That(SaveFormat.TryMigrate(save, out _), Is.True);
            Assert.That(save.version, Is.EqualTo(2));
            Assert.That(save.economy.started, Is.False);
            Assert.That(SaveFormat.Validate(save, id => id == "spruce"), Is.Empty);
            Assert.That(SaveFormat.TryRestore(save.economy, new Ledger(), new FastenerStock()), Is.False);
        }

        [Test]
        public void Economy_RoundTrips()
        {
            var ledger = new Ledger();
            ledger.Start(Money.FromEuros(100m));
            ledger.TrySpend(Money.FromEuros(8.50m), "1 x board");
            var fasteners = new FastenerStock();
            fasteners.Add("wood-screw-4x40", 188);

            SaveGameData save = ValidSave();
            save.economy = SaveFormat.ToData(ledger, fasteners);
            Assert.That(SaveFormat.Validate(save, id => id == "spruce"), Is.Empty);

            var restoredLedger = new Ledger();
            var restoredFasteners = new FastenerStock();
            Assert.That(SaveFormat.TryRestore(save.economy, restoredLedger, restoredFasteners), Is.True);
            Assert.That(restoredLedger.Balance, Is.EqualTo(Money.FromEuros(91.50m)));
            Assert.That(restoredLedger.Entries[1].Kind, Is.EqualTo(TransactionKind.Purchase));
            Assert.That(restoredLedger.Entries[1].Description, Is.EqualTo("1 x board"));
            Assert.That(restoredFasteners.Count("wood-screw-4x40"), Is.EqualTo(188));
        }

        [Test]
        public void Validate_InvalidEconomy()
        {
            SaveGameData save = ValidSave();
            save.economy = new EconomyData
            {
                started = true,
                transactions = new[]
                {
                    new TransactionData { kind = 0, amountCents = 500 },
                    new TransactionData { kind = 1, amountCents = -600 }
                },
                fasteners = new[]
                {
                    new FastenerCountData { fastenerId = "s", count = 1 },
                    new FastenerCountData { fastenerId = "s", count = 2 }
                }
            };
            Assert.That(SaveFormat.Validate(save).Count, Is.EqualTo(2));

            save.economy = new EconomyData { started = true, transactions = new[] { new TransactionData { kind = 7 } } };
            Assert.That(SaveFormat.Validate(save).Count, Is.EqualTo(1));

            save.economy = new EconomyData { started = false, fasteners = new[] { new FastenerCountData { fastenerId = "s" } } };
            Assert.That(SaveFormat.Validate(save).Count, Is.EqualTo(1));
        }

        [Test]
        public void Validate_DuplicateBoardIds()
        {
            SaveGameData save = ValidSave();
            save.boards[1].id = "b1";

            Assert.That(SaveFormat.Validate(save).Any(e => e.Contains("duplicated")), Is.True);
        }

        [Test]
        public void Validate_UnknownSpecies_WhenResolverGiven()
        {
            SaveGameData save = ValidSave();
            save.boards[0].speciesId = "balsa";

            Assert.That(SaveFormat.Validate(save, id => id == "spruce").Any(e => e.Contains("unknown species")), Is.True);
            Assert.That(SaveFormat.Validate(save), Is.Empty); // without resolver only the presence is checked
        }

        [Test]
        public void Validate_NonPositiveDimensions()
        {
            SaveGameData save = ValidSave();
            save.boards[0].widthTenths = 0;

            Assert.That(SaveFormat.Validate(save).Any(e => e.Contains("dimensions")), Is.True);
        }

        [TestCase(-1L, 0, 0)]
        [TestCase(6001L, 0, 0)]  // beyond the 600 mm board
        [TestCase(100L, 2, 0)]   // undefined BoardEnd
        [TestCase(100L, 0, 5)]   // undefined BoardFace
        public void Validate_InvalidMarks(long position, int measuredFrom, int face)
        {
            SaveGameData save = ValidSave();
            save.boards[0].marks = new[] { new MarkData { positionFromStartTenths = position, measuredFrom = measuredFrom, face = face } };

            Assert.That(SaveFormat.Validate(save).Any(e => e.Contains("pencil mark")), Is.True);
        }

        [Test]
        public void Validate_NonFinitePose()
        {
            SaveGameData save = ValidSave();
            save.boards[0].pose.px = float.NaN;

            Assert.That(SaveFormat.Validate(save).Any(e => e.Contains("finite")), Is.True);
        }

        [Test]
        public void Validate_BeltReferencesUnknownItem_OrEmptySelection()
        {
            SaveGameData save = ValidSave();
            save.belt.slots[1] = "Hammer";
            save.belt.selected = 2; // empty slot

            var errors = SaveFormat.Validate(save);
            Assert.That(errors.Any(e => e.Contains("unknown item 'Hammer'")), Is.True);
            Assert.That(errors.Any(e => e.Contains("selection")), Is.True);
        }

        [Test]
        public void Validate_InstalledPartWithUnknownBoard_OrInstalledTwice()
        {
            SaveGameData save = ValidSave();
            save.assemblies[0].parts = new[]
            {
                new InstalledPartData { slotId = "front", boardId = "b1" },
                new InstalledPartData { slotId = "back", boardId = "b1" },
                new InstalledPartData { slotId = "left", boardId = "ghost" }
            };

            var errors = SaveFormat.Validate(save);
            Assert.That(errors.Any(e => e.Contains("installed twice")), Is.True);
            Assert.That(errors.Any(e => e.Contains("unknown board")), Is.True);
        }

        [Test]
        public void Validate_NegativeWaste()
        {
            SaveGameData save = ValidSave();
            save.waste.volumeCubicMeters = -1;

            Assert.That(SaveFormat.Validate(save).Any(e => e.Contains("Waste")), Is.True);
        }

        [Test]
        public void Validate_InfiniteWaste()
        {
            SaveGameData save = ValidSave();
            save.waste.massKg = double.PositiveInfinity;

            Assert.That(SaveFormat.Validate(save).Any(e => e.Contains("Waste")), Is.True);
        }

        [Test]
        public void Validate_AssemblyProblems()
        {
            SaveGameData save = ValidSave();
            save.assemblies[0].blueprintId = "";
            save.assemblies[0].fastened[0].points = new[] { 0, 0 };

            var errors = SaveFormat.Validate(save);
            Assert.That(errors.Any(e => e.Contains("blueprint is missing")), Is.True);
            Assert.That(errors.Any(e => e.Contains("invalid fastening points")), Is.True);
        }

        [Test]
        public void Validate_TwoBoardsInOneSlot()
        {
            SaveGameData save = ValidSave();
            save.assemblies[0].parts = new[]
            {
                new InstalledPartData { slotId = "front", boardId = "b1" },
                new InstalledPartData { slotId = "front", boardId = "b2" }
            };

            Assert.That(SaveFormat.Validate(save).Any(e => e.Contains("holds two boards")), Is.True);
        }

        [Test]
        public void Validate_SwitchAndCutListIds()
        {
            SaveGameData save = ValidSave();
            save.switches = new[] { new SwitchData { id = "LightSwitch" }, new SwitchData { id = "LightSwitch" } };
            save.cutLists = new[] { new CutListData { id = "CutList", selectedIndex = -2 } };

            var errors = SaveFormat.Validate(save);
            Assert.That(errors.Any(e => e.StartsWith("Switch id")), Is.True);
            Assert.That(errors.Any(e => e.Contains("cut list 'CutList'")), Is.True);
        }

        [Test]
        public void Validate_JobData()
        {
            SaveGameData save = ValidSave();
            save.job = new JobData { activeJobId = "shelf", boardsDelivered = 3, boardsOrderedExtra = 1, elapsedSeconds = 90 };
            Assert.That(SaveFormat.Validate(save), Is.Empty);

            save.job.boardsOrderedExtra = 4; // more extra boards than delivered
            Assert.That(SaveFormat.Validate(save).Any(e => e.StartsWith("Job data")), Is.True);

            save.job = new JobData { elapsedSeconds = double.NaN };
            Assert.That(SaveFormat.Validate(save).Any(e => e.StartsWith("Job data")), Is.True);
        }

        [Test]
        public void Validate_HeadHeightsMustMatchThePoints()
        {
            SaveGameData save = ValidSave();
            save.assemblies[0].fastened[0].headHeightsTenths = new long[] { 0, 5 };

            Assert.That(SaveFormat.Validate(save).Any(e => e.Contains("invalid fastening points")), Is.True);

            save.assemblies[0].fastened[0].headHeightsTenths = new long[] { 12 };
            save.assemblies[0].fastened[0].pilotHoles = new[] { 0, 1 };
            Assert.That(SaveFormat.Validate(save), Is.Empty);
        }

        [Test]
        public void Board_RoundTrip_KeepsDimensionsAndMarksExactly()
        {
            var piece = new TimberPiece(Spruce, BoardDimensions.FromMillimeters(1498.3, 95, 45));
            var marks = new BoardMarks(piece.Dimensions.Length);
            marks.Add(new PencilMark(Mm(600), BoardEnd.Start, BoardFace.Top));
            marks.Add(new PencilMark(Mm(1210.7), BoardEnd.End, BoardFace.Bottom));

            BoardData data = SaveFormat.ToData("b7", piece, marks);
            TimberPiece restored = SaveFormat.ToPiece(data, Spruce, out BoardMarks restoredMarks);

            Assert.That(data.speciesId, Is.EqualTo("spruce"));
            Assert.That(restored.Dimensions, Is.EqualTo(piece.Dimensions));
            Assert.That(restoredMarks.Count, Is.EqualTo(2));
            Assert.That(restoredMarks.All[1].PositionFromStart, Is.EqualTo(Mm(1210.7)));
            Assert.That(restoredMarks.All[1].MeasuredFrom, Is.EqualTo(BoardEnd.End));
            Assert.That(restoredMarks.All[1].Face, Is.EqualTo(BoardFace.Bottom));
        }
    }
}
