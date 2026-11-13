using UnityEngine;

namespace WoodcraftSimulator.Core
{
    /// <summary>
    /// Player-facing settings persisted with <see cref="PlayerPrefs"/> (registry on Windows).
    /// For small preferences only; game progress goes into the versioned save system (Phase 7+).
    /// Stateless wrapper: PlayerPrefs is the single source of truth.
    /// </summary>
    public static class UserPreferences
    {
        public const float MinMouseSensitivity = 0.02f;
        public const float MaxMouseSensitivity = 0.5f;
        public const float DefaultMouseSensitivity = 0.1f;

        private const string MouseSensitivityKey = "WoodcraftSimulator.MouseSensitivity";

        /// <summary>Mouse look speed in degrees per pixel of mouse movement.</summary>
        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity),
                MinMouseSensitivity, MaxMouseSensitivity);
            set => PlayerPrefs.SetFloat(MouseSensitivityKey,
                Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity));
        }

        /// <summary>Writes pending changes to disk. Unity also saves automatically on a clean quit.</summary>
        public static void Save() => PlayerPrefs.Save();
    }
}
