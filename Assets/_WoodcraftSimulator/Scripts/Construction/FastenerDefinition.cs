using UnityEngine;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Construction
{
    /// <summary>
    /// A fastener type as data (ScriptableObject asset), e.g. a 5 × 80 mm wood screw. Generic sizes only: no product
    /// names, prices or certifications (master prompt §6.8). Converted to the domain <see cref="FastenerSpec"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "WoodcraftSimulator/Fastener Definition", fileName = "Fastener")]
    public sealed class FastenerDefinition : ScriptableObject
    {
        [SerializeField] private string id = "wood-screw-5x80";
        [SerializeField] private string displayName = "Wood screw 5 × 80 mm";
        [SerializeField, Min(0.1f)] private float diameterMillimeters = 5f;
        [SerializeField, Min(1f)] private float lengthMillimeters = 80f;

        public string Id => id;

        public string DisplayName => displayName;

        public FastenerSpec ToDomain() => new FastenerSpec(id, displayName,
            Length.FromMillimeters((double)diameterMillimeters), Length.FromMillimeters((double)lengthMillimeters));
    }
}
