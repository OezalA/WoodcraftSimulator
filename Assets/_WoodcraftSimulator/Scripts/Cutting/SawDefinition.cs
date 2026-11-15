using UnityEngine;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Cutting
{
    /// <summary>
    /// Static data of a saw type (ScriptableObject asset). Converted to the domain <see cref="Saw"/> for cutting rules.
    /// Values are approximate placeholders until verified (D-029).
    /// </summary>
    [CreateAssetMenu(menuName = "WoodcraftSimulator/Saw Definition", fileName = "Saw")]
    public sealed class SawDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier, e.g. \"hand-saw\".")]
        [SerializeField] private string id = "hand-saw";

        [SerializeField] private string displayName = "Hand saw";

        [Tooltip("Turkish name (D-041); empty = the English name is shown.")]
        [SerializeField] private string displayNameTurkish = string.Empty;

        [Tooltip("Width of material turned into sawdust per cut, in millimeters.")]
        [SerializeField, Min(0.1f)] private float kerfMillimeters = 2f;

        [Tooltip("Gameplay sawing time per square centimeter of cross-section (95 × 45 mm ≈ 43 cm² → 3 s at 0.07).")]
        [SerializeField, Min(0.001f)] private float secondsPerSquareCentimeter = 0.07f;

        [Tooltip("Shortest and longest sawing time in seconds, whatever the cross-section.")]
        [SerializeField] private Vector2 secondsRange = new Vector2(1.5f, 8f);

        public string Id => id;

        public string DisplayName => Localization.Loc.Pick(displayName, displayNameTurkish);

        public Saw ToDomain() => new Saw(id, DisplayName, Length.FromMillimeters((double)kerfMillimeters));

        /// <summary>Time to saw through a cross-section of the given area (gameplay value, not physics).</summary>
        public float SawingSeconds(double crossSectionSquareCentimeters) =>
            Mathf.Clamp((float)crossSectionSquareCentimeters * secondsPerSquareCentimeter, secondsRange.x, secondsRange.y);
    }
}
