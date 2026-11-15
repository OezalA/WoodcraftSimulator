using System;

namespace WoodcraftSimulator.Localization
{
    public enum Language
    {
        English,
        Turkish
    }

    /// <summary>
    /// In-game text language (D-041). Two languages, so every player-facing text is written as an English/Turkish
    /// pair right where it is used: <c>Loc.T("Pilot hole drilled", "Pilot delik açıldı")</c>. Nothing can be left
    /// untranslated by accident, and the code stays readable. Names in data assets carry their own Turkish field and
    /// use <see cref="Pick"/>. If a third language is ever needed, this becomes a key-based string table.
    /// The default here is English (tests); the game sets the player's choice at startup (default Turkish).
    /// </summary>
    public static class Loc
    {
        public static Language Current { get; set; } = Language.English;

        public static bool IsTurkish => Current == Language.Turkish;

        /// <summary>The text in the current language.</summary>
        public static string T(string english, string turkish) => IsTurkish ? turkish : english;

        /// <summary>A data name in the current language; falls back to English when no Turkish text was entered.</summary>
        public static string Pick(string english, string turkish) =>
            IsTurkish && !string.IsNullOrWhiteSpace(turkish) ? turkish : english;

        /// <summary>Runs <paramref name="action"/> with another language, then restores the previous one (tests, tools).</summary>
        public static void With(Language language, Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            Language previous = Current;
            Current = language;
            try
            {
                action();
            }
            finally
            {
                Current = previous;
            }
        }
    }
}
