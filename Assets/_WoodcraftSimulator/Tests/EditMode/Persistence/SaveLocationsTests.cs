using NUnit.Framework;
using WoodcraftSimulator.Persistence;

namespace WoodcraftSimulator.Tests.Persistence
{
    /// <summary>One save for every place (D-056).</summary>
    public sealed class SaveLocationsTests
    {
        private static BoardData Board(string id, string location) => new BoardData { id = id, location = location };

        [Test]
        public void NoLocation_IsTheWorkshop()
        {
            Assert.That(SaveLocations.Of(null), Is.EqualTo("workshop"));
            Assert.That(SaveLocations.Of(string.Empty), Is.EqualTo("workshop"));
            Assert.That(SaveLocations.Of("mountain"), Is.EqualTo("mountain"));
        }

        [Test]
        public void Replace_KeepsOtherPlaces_SwapsThisOne()
        {
            BoardData[] all = { Board("a", null), Board("b", "mountain"), Board("c", "workshop") };

            BoardData[] replaced = SaveLocations.Replace(all, "workshop", b => b.location, new[] { Board("d", "workshop") });

            Assert.That(replaced, Has.Length.EqualTo(2));
            Assert.That(replaced[0].id, Is.EqualTo("b"), "The mountain's board stays.");
            Assert.That(replaced[1].id, Is.EqualTo("d"));
            Assert.That(SaveLocations.At(all, "workshop", b => b.location), Has.Count.EqualTo(2), "An old entry is the workshop's.");
        }
    }
}
