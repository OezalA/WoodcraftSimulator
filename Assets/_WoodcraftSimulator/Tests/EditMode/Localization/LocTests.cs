using NUnit.Framework;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Tests.Localization
{
    public sealed class LocTests
    {
        [Test]
        public void T_FollowsTheCurrentLanguage()
        {
            Assert.That(Loc.Current, Is.EqualTo(Language.English), "Tests run in English by default.");
            Assert.That(Loc.T("Saw", "Testere"), Is.EqualTo("Saw"));
            Loc.With(Language.Turkish, () => Assert.That(Loc.T("Saw", "Testere"), Is.EqualTo("Testere")));
            Assert.That(Loc.Current, Is.EqualTo(Language.English));
        }

        [Test]
        public void Pick_FallsBackToEnglishWhenTurkishIsEmpty()
        {
            Loc.With(Language.Turkish, () =>
            {
                Assert.That(Loc.Pick("Spruce", "Ladin"), Is.EqualTo("Ladin"));
                Assert.That(Loc.Pick("Spruce", ""), Is.EqualTo("Spruce"));
                Assert.That(Loc.Pick("Spruce", null), Is.EqualTo("Spruce"));
            });
            Assert.That(Loc.Pick("Spruce", "Ladin"), Is.EqualTo("Spruce"));
        }

        [Test]
        public void DomainTexts_AreTurkish()
        {
            Loc.With(Language.Turkish, () =>
            {
                var stock = new StockSpec("spruce", "Ladin", BoardDimensions.FromMillimeters(2000, 145, 18));
                Assert.That(stock.ToString(), Is.EqualTo("Ladin tahta 2000 × 145 × 18 mm"));

                var ledger = new Ledger();
                ledger.Start(Money.FromEuros(100m));
                Assert.That(ledger.Entries[0].Description, Is.EqualTo("Başlangıç parası"));

                string inventory = WorkshopInventory.Format(ledger, new InventoryGroup[0], new FastenerStock());
                StringAssert.StartsWith("ATÖLYE ENVANTERİ\nPara: €100.00", inventory);
            });
        }
    }
}
