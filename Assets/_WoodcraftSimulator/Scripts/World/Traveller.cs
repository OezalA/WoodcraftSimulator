using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Inventory;

namespace WoodcraftSimulator.World
{
    /// <summary>
    /// A movable object that can be at any place: the tools and the stepladder (D-056). It lives in the core scene,
    /// so it is never lost when a place unloads; it remembers the place it was put down at and is shown only while
    /// that place is loaded. On the belt or in the hands it is with the player (the belt / carrier show it).
    /// </summary>
    public sealed class Traveller : MonoBehaviour
    {
        private static readonly List<Traveller> AllTravellers = new List<Traveller>();

        [Tooltip("The place it lies at (a location id).")]
        [SerializeField] private string location = LocationDefinition.WorkshopId;

        private PickupItem item;
        private Carryable carryable;

        public static IReadOnlyList<Traveller> All => AllTravellers;

        public string Location
        {
            get => string.IsNullOrEmpty(location) ? LocationDefinition.WorkshopId : location;
            set => location = string.IsNullOrEmpty(value) ? LocationDefinition.WorkshopId : value;
        }

        /// <summary>On the belt or in the hands: with the player wherever he goes.</summary>
        public bool IsWithPlayer => (item != null && item.IsStored) || (carryable != null && carryable.IsCarried);

        private void Awake()
        {
            TryGetComponent(out item);
            TryGetComponent(out carryable);
            AllTravellers.Add(this);
        }

        private void OnDestroy() => AllTravellers.Remove(this);

        /// <summary>Shows or hides each object lying somewhere by whether its place is the one loaded now.</summary>
        public static void ShowFor(string currentLocation)
        {
            foreach (Traveller traveller in AllTravellers)
            {
                if (traveller != null && !traveller.IsWithPlayer)
                {
                    traveller.gameObject.SetActive(traveller.Location == currentLocation);
                }
            }
        }

        /// <summary>Put down here: it belongs to the place the player is at now.</summary>
        public void PutDownHere()
        {
            SceneFlow flow = Core.Services.Get<SceneFlow>();
            Location = flow != null ? flow.CurrentLocationId : LocationDefinition.WorkshopId;
        }
    }
}
