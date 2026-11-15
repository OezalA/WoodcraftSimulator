using NUnit.Framework;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Tests
{
    /// <summary>
    /// Tests expect English texts. In the Unity Editor the language set for Play Mode (D-041) survives into Edit
    /// Mode, so every test run starts in English.
    /// </summary>
    [SetUpFixture]
    public sealed class TestLanguage
    {
        [OneTimeSetUp]
        public void UseEnglish() => Loc.Current = Language.English;
    }
}
