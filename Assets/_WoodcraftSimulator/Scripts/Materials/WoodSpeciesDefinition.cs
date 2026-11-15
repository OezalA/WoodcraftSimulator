using UnityEngine;

namespace WoodcraftSimulator.Materials
{
    /// <summary>
    /// Static configuration of a wood species (ScriptableObject asset, edited in the Inspector).
    /// Converted to the immutable domain type <see cref="WoodSpecies"/> for game rules.
    /// Never stores runtime state (master prompt §6.16).
    /// </summary>
    [CreateAssetMenu(menuName = "WoodcraftSimulator/Wood Species", fileName = "WoodSpecies")]
    public sealed class WoodSpeciesDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier used by data and save files. Do not change after release.")]
        [SerializeField] private string id = "species";

        [SerializeField] private string displayName = "Species";

        [Tooltip("Turkish name (D-041); empty = the English name is shown.")]
        [SerializeField] private string displayNameTurkish = string.Empty;

        [SerializeField] private string scientificName = "";

        [Tooltip("Typical density at ~12 % moisture content (air-dry), kg/m³. Approximate reference value.")]
        [SerializeField, Min(1f)] private float densityKgPerCubicMeter = 500f;

        [Tooltip("False for non-wood building materials handled like boards, e.g. concrete pier blocks (D-046).")]
        [SerializeField] private bool isTimber = true;

        [Tooltip("Flat color used until real wood textures exist.")]
        [SerializeField] private Color greyboxColor = new Color(0.85f, 0.72f, 0.52f);

        [Tooltip("Where the values come from and how reliable they are.")]
        [SerializeField, TextArea] private string dataNotes = "";

        public string Id => id;

        public string DisplayName => Localization.Loc.Pick(displayName, displayNameTurkish);

        public string ScientificName => scientificName;

        public Color GreyboxColor => greyboxColor;

        public string DataNotes => dataNotes;

        public WoodSpecies ToDomain() => new WoodSpecies(id, DisplayName, densityKgPerCubicMeter, isTimber);
    }
}
