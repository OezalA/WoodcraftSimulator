using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;
using WoodcraftSimulator.Persistence;

namespace WoodcraftSimulator.World
{
    /// <summary>
    /// The van (Phase 12.5b, D-056): one stands at the workshop and at every customer location. E drives to the place
    /// chosen with the wheel: the workshop, or the place of the active job. What lies in its cargo bed travels along
    /// (boards, tools, the stepladder) and is unloaded at the same spot of the other van; the tools on the belt are
    /// always with the player. Leaving a customer's place, tools and the stepladder still lying there are packed
    /// into the van (they are never left behind; boards are, with the job). A trip from the workshop to a customer
    /// costs its fuel or ferry (there and back), charged like the rent (may make debt, D-054).
    /// </summary>
    public sealed class Van : MonoBehaviour, IScrollable, IDescribable, IInteractionHint
    {
        private static readonly List<Van> Loaded = new List<Van>();

        // What travels between two vans: boards as save data, travellers as they are; poses relative to the cargo bed.
        private static readonly List<(BoardData board, Vector3 position, Quaternion rotation)> BoardCargo =
            new List<(BoardData, Vector3, Quaternion)>();

        private static readonly List<(Traveller traveller, Vector3 position, Quaternion rotation)> ThingCargo =
            new List<(Traveller, Vector3, Quaternion)>();

        [Tooltip("Trigger box over the cargo bed: what lies in it travels along.")]
        [SerializeField] private BoxCollider cargoArea;

        private int selected;
        private string message;
        private float messageUntil;

        public string Description => Loc.T("Van", "Kamyonet");

        public string InteractionPrompt
        {
            get
            {
                List<LocationDefinition> destinations = Destinations();
                string text;
                if (destinations.Count == 0)
                {
                    text = Loc.T("Van: nowhere to drive (take a job at a customer's place first)",
                        "Kamyonet: gidecek yer yok (önce bir müşteri işi al)");
                }
                else
                {
                    LocationDefinition target = destinations[Mathf.Clamp(selected, 0, destinations.Count - 1)];
                    Economy.Money cost = TripCost(target);
                    string costText = cost > Economy.Money.Zero ? Loc.T($", trip {cost}", $", yol {cost}") : string.Empty;
                    text = Loc.T($"Drive to {target.DisplayName}{costText}", $"{target.DisplayName} yoluna çık{costText}");
                }

                return message != null && Time.unscaledTime < messageUntil ? $"{text}\n{message}" : text;
            }
        }

        public string InteractionHint => Destinations().Count > 1
            ? Loc.T("[Wheel]  choose where to", "[Tekerlek]  nereye gideceğini seç")
            : Loc.T("Load the bed: what lies in it comes along", "Kasaya yükle: içindekiler seninle gelir");

        private void OnEnable() => Loaded.Add(this);

        private void OnDisable() => Loaded.Remove(this);

        public void Scroll(int steps)
        {
            int count = Destinations().Count;
            if (count > 0)
            {
                selected = ((selected + steps) % count + count) % count;
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            SceneFlow flow = Core.Services.Get<SceneFlow>();
            List<LocationDefinition> destinations = Destinations();
            if (flow == null || destinations.Count == 0)
            {
                return;
            }

            if (interactor.TryGetComponent(out PlayerCarrier carrier) && carrier.IsCarrying)
            {
                Say(Loc.T("Put down what you carry first (or load it in the bed)", "Önce elindekini bırak (ya da kasaya koy)"));
                return;
            }

            LocationDefinition target = destinations[Mathf.Clamp(selected, 0, destinations.Count - 1)];
            Economy.Money cost = TripCost(target);
            if (cost > Economy.Money.Zero)
            {
                Core.Services.Get<WorkshopEconomy>()?.Ledger.Charge(cost,
                    Loc.T($"Trip: {target.DisplayName}", $"Yol: {target.DisplayName}"));
            }

            string leaving = flow.CurrentLocationId;
            flow.Travel(target, scene => Pack(scene, leaving, target.Id), Unpack);
        }

        /// <summary>The workshop (when away) and the place of the active job (when not there).</summary>
        private List<LocationDefinition> Destinations()
        {
            var result = new List<LocationDefinition>();
            SceneFlow flow = Core.Services.Get<SceneFlow>();
            if (flow == null || !flow.IsReady)
            {
                return result;
            }

            JobBoard jobs = Core.Services.Get<JobBoard>();
            string jobPlace = jobs != null ? jobs.ActiveJobLocation : null;
            foreach (LocationDefinition location in flow.Locations)
            {
                if (location == null || location == flow.Current)
                {
                    continue;
                }

                if (location.IsWorkshop || location.Id == jobPlace)
                {
                    result.Add(location);
                }
            }

            return result;
        }

        /// <summary>Leaving the workshop for a customer costs the trip there and back; coming home is paid already.</summary>
        private static Economy.Money TripCost(LocationDefinition target)
        {
            SceneFlow flow = Core.Services.Get<SceneFlow>();
            bool fromWorkshop = flow == null || flow.Current == null || flow.Current.IsWorkshop;
            return fromWorkshop && !target.IsWorkshop ? target.TripCost : Economy.Money.Zero;
        }

        /// <summary>Before the place is captured and unloaded: what is in the bed (and, at a customer's, every tool) goes in the van.</summary>
        private void Pack(Scene leaving, string leavingId, string targetId)
        {
            BoardCargo.Clear();
            ThingCargo.Clear();
            Transform bed = cargoArea != null ? cargoArea.transform : transform;
            Bounds area = cargoArea != null ? cargoArea.bounds : new Bounds(transform.position, Vector3.zero);

            foreach (TimberBoard board in FindObjectsByType<TimberBoard>(FindObjectsSortMode.None))
            {
                if (board.gameObject.scene != leaving || board.Piece == null || !board.TryGetComponent(out Carryable carryable) ||
                    carryable.InstalledIn != null || carryable.IsCarried || !area.Contains(board.transform.position) ||
                    !board.TryGetComponent(out PersistentId id))
                {
                    continue;
                }

                BoardData data = SaveFormat.ToData(id.Id, board.Piece, board.Marks);
                data.surface = (int)board.Surface;
                BoardCargo.Add((data, bed.InverseTransformPoint(board.transform.position),
                    Quaternion.Inverse(bed.rotation) * board.transform.rotation));
                // At once (not at the end of the frame): the place is captured right after and must not keep it.
                DestroyImmediate(board.gameObject);
            }

            bool customerPlace = leavingId != LocationDefinition.WorkshopId;
            int packed = 0;
            foreach (Traveller traveller in Traveller.All)
            {
                if (traveller == null || traveller.IsWithPlayer || traveller.Location != leavingId)
                {
                    continue;
                }

                bool inBed = area.Contains(traveller.transform.position);
                if (!inBed && !customerPlace)
                {
                    continue;
                }

                // Lying in the bed keeps its spot; packed from around the site goes in a row on the bed.
                Vector3 local = inBed ? bed.InverseTransformPoint(traveller.transform.position) : PackingSpot(packed++);
                Quaternion rotation = inBed ? Quaternion.Inverse(bed.rotation) * traveller.transform.rotation : Quaternion.identity;
                ThingCargo.Add((traveller, local, rotation));
                traveller.Location = targetId;
            }
        }

        /// <summary>After the new place loaded: the cargo comes out of this place's van at the same spots.</summary>
        private static void Unpack(Scene arrived)
        {
            Van van = Loaded.Find(each => each != null && each.gameObject.scene == arrived);
            Transform bed = van != null && van.cargoArea != null ? van.cargoArea.transform : van != null ? van.transform : null;
            TimberBoardFactory factory = Core.Services.Get<TimberBoardFactory>();
            if (bed == null)
            {
                Debug.LogWarning("[Van] No van at the place arrived at: the cargo stays where it was.");
                BoardCargo.Clear();
                ThingCargo.Clear();
                return;
            }

            foreach ((BoardData data, Vector3 position, Quaternion rotation) in BoardCargo)
            {
                if (factory == null || !factory.TryGetSpecies(data.speciesId, out WoodSpeciesDefinition species))
                {
                    continue;
                }

                TimberPiece piece = SaveFormat.ToPiece(data, species.ToDomain(), out BoardMarks marks);
                TimberBoard board = factory.Create(data.id, species, piece, marks, bed.TransformPoint(position),
                    bed.rotation * rotation);
                board.SetSurface((BoardSurface)data.surface);
                board.GetComponent<Carryable>().RequestSupportCheck();
            }

            foreach ((Traveller traveller, Vector3 position, Quaternion rotation) in ThingCargo)
            {
                if (traveller != null)
                {
                    traveller.transform.SetPositionAndRotation(bed.TransformPoint(position), bed.rotation * rotation);
                    if (traveller.TryGetComponent(out Carryable carryable))
                    {
                        carryable.RequestSupportCheck();
                    }
                }
            }

            BoardCargo.Clear();
            ThingCargo.Clear();
        }

        // Rows across the bed, from the cab backwards, a little above its floor (they settle down).
        private Vector3 PackingSpot(int index)
        {
            Vector3 size = cargoArea != null ? cargoArea.size : new Vector3(1.6f, 1f, 2.6f);
            int perRow = 3;
            float x = (index % perRow - 1) * size.x / 3.5f;
            float z = size.z / 2f - 0.3f - (index / perRow) * 0.45f;
            return new Vector3(x, -size.y / 2f + 0.08f, z);
        }

        private void Say(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 3f;
        }
    }
}
