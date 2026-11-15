using UnityEngine;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Core
{
    /// <summary>
    /// Player-facing settings persisted with <see cref="PlayerPrefs"/> (registry on Windows).
    /// For small preferences only; game progress goes into the versioned save system.
    /// Stateless wrapper: PlayerPrefs is the single source of truth.
    /// </summary>
    public static class UserPreferences
    {
        public const float MinMouseSensitivity = 0.02f;
        public const float MaxMouseSensitivity = 0.5f;
        public const float DefaultMouseSensitivity = 0.1f;

        /// <summary>The owner's choice (D-041): Turkish unless the player picks English.</summary>
        public const Language DefaultLanguage = Language.Turkish;

        private const string MouseSensitivityKey = "WoodcraftSimulator.MouseSensitivity";
        private const string LanguageKey = "WoodcraftSimulator.Language";

        /// <summary>Mouse look speed in degrees per pixel of mouse movement.</summary>
        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity),
                MinMouseSensitivity, MaxMouseSensitivity);
            set => PlayerPrefs.SetFloat(MouseSensitivityKey,
                Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity));
        }

        /// <summary>In-game text language. Takes effect when the scene is (re)loaded.</summary>
        public static Language Language
        {
            get
            {
                int stored = PlayerPrefs.GetInt(LanguageKey, (int)DefaultLanguage);
                return System.Enum.IsDefined(typeof(Language), stored) ? (Language)stored : DefaultLanguage;
            }
            set => PlayerPrefs.SetInt(LanguageKey, (int)value);
        }

        /// <summary>Writes pending changes to disk. Unity also saves automatically on a clean quit.</summary>
        public static void Save() => PlayerPrefs.Save();

        // Before any scene object wakes up, so names built in Awake (species, blueprints, jobs) use the language.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyLanguage() => Loc.Current = Language;
    }
}
