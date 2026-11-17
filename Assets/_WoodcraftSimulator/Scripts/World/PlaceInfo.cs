using UnityEngine;

namespace WoodcraftSimulator.World
{
    /// <summary>
    /// Marks a content scene as a place (D-056): which location it is and where the player arrives (by the van).
    /// One per content scene; the core scene's <see cref="SceneFlow"/> finds it when the scene has loaded.
    /// </summary>
    public sealed class PlaceInfo : MonoBehaviour
    {
        [SerializeField] private LocationDefinition location;

        [Tooltip("Where the player stands after arriving (next to the van); the forward axis is the view.")]
        [SerializeField] private Transform arrivalPoint;

        public LocationDefinition Location => location;

        public string Id => location != null ? location.Id : LocationDefinition.WorkshopId;

        public Transform ArrivalPoint => arrivalPoint != null ? arrivalPoint : transform;
    }
}
