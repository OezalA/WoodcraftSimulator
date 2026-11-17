using NUnit.Framework;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Tests.Materials
{
    /// <summary>Offcut bins sorted by species (D-052).</summary>
    public sealed class OffcutStockTests
    {
        private static readonly WoodSpecies Spruce = new WoodSpecies("spruce", "Spruce", 450);
        private static readonly WoodSpecies Oak = new WoodSpecies("oak", "Oak", 700);
        private static readonly WoodSpecies Concrete = new WoodSpecies("concrete", "Concrete", 2200, isTimber: false);

        private static TimberPiece Piece(WoodSpecies species, double length, double width = 145, double thickness = 18) =>
            new TimberPiece(species, BoardDimensions.FromMillimeters(length, width, thickness));

        [Test]
        public void Bin_TakesOnlyTimberOfItsSpecies()
        {
            var bin = new OffcutStock("spruce");

            Assert.That(bin.TryAdd(Piece(Spruce, 450)), Is.True);
            Assert.That(bin.TryAdd(Piece(Oak, 450)), Is.False);
            Assert.That(bin.TryAdd(new TimberPiece(new WoodSpecies("spruce", "Spruce", 450, isTimber: false),
                BoardDimensions.FromMillimeters(200, 200, 200))), Is.False, "Not timber.");
            Assert.That(bin.TryAdd(Piece(Concrete, 200, 200, 200)), Is.False);
            Assert.That(bin.Count, Is.EqualTo(1));
        }

        [Test]
        public void Groups_SameSizeAndSurfaceTogether_LongestFirst()
        {
            var bin = new OffcutStock("spruce");
            bin.TryAdd(Piece(Spruce, 300));
            bin.TryAdd(Piece(Spruce, 450));
            bin.TryAdd(Piece(Spruce, 300));
            bin.TryAdd(Piece(Spruce, 450), BoardSurface.Sanded);
            bin.TryAdd(Piece(Spruce, 450, 95, 45));

            var groups = bin.Groups();

            Assert.That(groups.Count, Is.EqualTo(4));
            Assert.That(groups[0].Dimensions, Is.EqualTo(BoardDimensions.FromMillimeters(450, 145, 18)));
            Assert.That(groups[0].Surface, Is.EqualTo(BoardSurface.Raw));
            Assert.That(groups[1].Surface, Is.EqualTo(BoardSurface.Sanded));
            Assert.That(groups[2].Dimensions, Is.EqualTo(BoardDimensions.FromMillimeters(450, 95, 45)));
            Assert.That(groups[3].Dimensions.Length.Millimeters, Is.EqualTo(300));
            Assert.That(groups[3].Count, Is.EqualTo(2));
        }

        [Test]
        public void Take_RemovesOnePieceOfTheGroup()
        {
            var bin = new OffcutStock("spruce");
            bin.TryAdd(Piece(Spruce, 300));
            bin.TryAdd(Piece(Spruce, 300));
            bin.TryAdd(Piece(Spruce, 450), BoardSurface.Oiled);
            int changes = 0;
            bin.Changed += () => changes++;

            Assert.That(bin.TryTake(1, out StoredPiece taken), Is.True);
            Assert.That(taken.Piece.Dimensions.Length.Millimeters, Is.EqualTo(300));
            Assert.That(bin.Groups()[1].Count, Is.EqualTo(1));

            Assert.That(bin.TryTake(0, out taken), Is.True);
            Assert.That(taken.Surface, Is.EqualTo(BoardSurface.Oiled), "The surface comes back with the piece.");

            Assert.That(bin.TryTake(5, out _), Is.False);
            Assert.That(bin.Count, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void Restore_ReplacesTheContents()
        {
            var bin = new OffcutStock("oak");
            bin.TryAdd(Piece(Oak, 600));
            bin.Restore(new[] { new StoredPiece(Piece(Oak, 250), BoardSurface.Raw), new StoredPiece(Piece(Spruce, 250), BoardSurface.Raw) });

            Assert.That(bin.Count, Is.EqualTo(1));
            Assert.That(bin.Pieces[0].Piece.Dimensions.Length.Millimeters, Is.EqualTo(250));
        }
    }
}
