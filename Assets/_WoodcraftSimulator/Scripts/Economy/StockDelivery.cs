using System;
using UnityEngine;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Economy
{
    /// <summary>
    /// The delivery area (D-040): bought boards are laid flat on the floor here, lengthwise along the forward axis,
    /// side by side along the right axis, stacked in layers when a row is full. Each board goes to the first spot
    /// where nothing lies yet, so boards already lying there (or moved there by the player) are not overlapped.
    /// </summary>
    public sealed class StockDelivery : MonoBehaviour
    {
        [SerializeField] private TimberBoardFactory boardFactory;

        [Tooltip("Which deliveries come here: \"workshop\", \"outdoor\" (the sites by the workshop) or a location id (D-056).")]
        [SerializeField] private string deliveryId = "workshop";

        private static readonly System.Collections.Generic.List<StockDelivery> Loaded =
            new System.Collections.Generic.List<StockDelivery>();

        public string DeliveryId => deliveryId;

        /// <summary>The loaded delivery area with this id, or null (its scene isn't loaded).</summary>
        public static StockDelivery Find(string id) => Loaded.Find(each => each != null && each.deliveryId == id);

        // The factory lives in the core scene (D-056).
        private void Awake() => boardFactory = Core.Services.Resolve(boardFactory);

        // Boards bought while this place wasn't loaded are brought now (D-056).
        private void OnEnable()
        {
            Loaded.Add(this);
            Core.Services.Get<WorkshopEconomy>()?.DeliverPending(this);
        }

        private void OnDisable() => Loaded.Remove(this);

        [SerializeField, Min(1)] private int boardsPerRow = 6;
        [SerializeField, Min(1)] private int maximumLayers = 8;
        [SerializeField, Min(0f)] private float gap = 0.03f;

        private readonly Collider[] overlapBuffer = new Collider[8];

        public void Deliver(WoodSpeciesDefinition species, BoardDimensions size, int count)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            if (boardFactory == null)
            {
                Debug.LogError($"{nameof(StockDelivery)}: Board Factory is not assigned.", this);
                return;
            }

            WoodSpecies domainSpecies = species.ToDomain();
            for (int i = 0; i < count; i++)
            {
                // Colliders created this frame must be visible to the free-spot check of the next board.
                Physics.SyncTransforms();
                Vector3 position = FreeSpot(size);
                var piece = new TimberPiece(domainSpecies, size);
                TimberBoard board = boardFactory.Create(Guid.NewGuid().ToString("N"), species, piece,
                    new BoardMarks(size.Length), position, transform.rotation);
                board.GetComponent<Carryable>().RequestSupportCheck();
            }
        }

        private Vector3 FreeSpot(BoardDimensions size)
        {
            float width = (float)size.Width.Meters;
            float thickness = (float)size.Thickness.Meters;
            float length = (float)size.Length.Meters;
            var halfExtents = new Vector3(width / 2f - 0.005f, thickness / 2f - 0.002f, length / 2f - 0.005f);
            Vector3 last = transform.position;
            for (int layer = 0; layer < maximumLayers; layer++)
            {
                for (int column = 0; column < boardsPerRow; column++)
                {
                    Vector3 position = transform.position +
                                       transform.right * (column * (width + gap)) +
                                       transform.up * (thickness * (layer + 0.5f) + 0.002f * (layer + 1));
                    last = position;
                    int hits = Physics.OverlapBoxNonAlloc(position, halfExtents, overlapBuffer, transform.rotation,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (!HitsTimber(hits))
                    {
                        return position;
                    }
                }
            }

            return last + transform.up * thickness;
        }

        private bool HitsTimber(int hits)
        {
            for (int i = 0; i < hits; i++)
            {
                if (overlapBuffer[i].GetComponentInParent<TimberBoard>() != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
