using UnityEngine;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.World
{
    /// <summary>
    /// A place the player can be (Phase 12.5, D-055/D-056): the workshop or a customer location. Each has its own
    /// content scene (loaded additively next to the core scene), a trip cost (fuel, ferry), and how the merchant
    /// treats it (material price factor, delivery fee).
    /// </summary>
    [CreateAssetMenu(menuName = "WoodcraftSimulator/Location", fileName = "Location")]
    public sealed class LocationDefinition : ScriptableObject
    {
        public const string WorkshopId = "workshop";

        [SerializeField] private string id = WorkshopId;
        [SerializeField] private string displayName = "Workshop";
        [SerializeField] private string displayNameTurkish = "Atölye";

        [Tooltip("The content scene, e.g. Assets/_WoodcraftSimulator/Scenes/Workshop/Workshop.unity.")]
        [SerializeField] private string scenePath = string.Empty;

        [Tooltip("Cost of a trip there and back (fuel, ferry), charged when leaving the workshop for it.")]
        [SerializeField, Min(0f)] private float tripCostEuros;

        [Tooltip("Material for jobs here costs this much more (e.g. on an island).")]
        [SerializeField, Min(1f)] private float materialPriceFactor = 1f;

        [Tooltip("The merchant's fee for bringing a job's material here.")]
        [SerializeField, Min(0f)] private float deliveryFeeEuros;

        public string Id => id;

        public string DisplayName => Loc.Pick(displayName, displayNameTurkish);

        public string ScenePath => scenePath;

        public Economy.Money TripCost => Economy.Money.FromEuros((decimal)System.Math.Round(tripCostEuros, 2));

        public float MaterialPriceFactor => materialPriceFactor;

        public Economy.Money DeliveryFee => Economy.Money.FromEuros((decimal)System.Math.Round(deliveryFeeEuros, 2));

        public bool IsWorkshop => id == WorkshopId;
    }
}
