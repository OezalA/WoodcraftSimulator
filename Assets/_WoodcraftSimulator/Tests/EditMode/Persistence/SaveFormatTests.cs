using System.Linq;
using NUnit.Framework;
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
