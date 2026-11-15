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

        [Tooltip("Width of material turned into sawdust per cut, in millimeters.")]
        [SerializeField, Min(0.1f)] private float kerfMillimeters = 2f;

        [Header("Strokes (D-038)")]
        [Tooltip("Saw travel (mm) per cm² of cross-section in spruce-like wood (18 × 145 mm ≈ 26 cm² → ~3100 mm ≈ 13 strokes of 250 mm).")]
        [SerializeField, Min(1f)] private float strokeMillimetersPerSquareCentimeter = 120f;

        [Tooltip("Usable blade length (mm): movement beyond it doesn't cut.")]
        [SerializeField, Min(50f)] private float bladeLengthMillimeters = 500f;

        [Tooltip("Share of the cut before the kerf guides the blade (sideways hand movement moves the blade).")]
        [SerializeField, Range(0f, 0.5f)] private float startFraction = 0.1f;

        [Tooltip("Strokes faster than this (mm/s) make the blade jump while starting.")]
        [SerializeField, Min(50f)] private float fastStrokeMillimetersPerSecond = 900f;

        public string Id => id;

        public string DisplayName => displayName;

        public Saw ToDomain() => new Saw(id, displayName, Length.FromMillimeters((double)kerfMillimeters));

        public float BladeLengthMillimeters => bladeLengthMillimeters;

        public SawingSettings SawingSettings() => new SawingSettings(strokeMillimetersPerSquareCentimeter,
            bladeLengthMillimeters, startFraction, fastStrokeMillimetersPerSecond: fastStrokeMillimetersPerSecond);
    }
}
