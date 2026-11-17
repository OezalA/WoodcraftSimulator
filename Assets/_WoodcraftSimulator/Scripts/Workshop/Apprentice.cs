using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// The apprentice (D-052): on command (E on him) he tidies up the loose offcuts: walks to the nearest one, carries
    /// it to the <see cref="OffcutBin"/> of its species, until none is left, then back to his place. E while he works
    /// stops him after the piece in hand. He leaves alone what is not an offcut: full stock boards, pieces with
    /// pencil marks (work in progress), pieces the active job still needs, pieces on the timber rack
    /// (<see cref="KeepPiecesHere"/>), installed or carried ones. Walks on the <see cref="RuntimeNavMesh"/>.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Apprentice : MonoBehaviour, IInteractable, IDescribable
    {
        private enum State
        {
            Idle,
            ToPiece,
            ToBin,
            Home
        }

        [Tooltip("Where a carried piece is held (its length along the apprentice's forward).")]
        [SerializeField] private Transform carryPoint;

        [SerializeField] private JobBoard jobBoard;
        [SerializeField] private WorkshopEconomy economy;

        [Tooltip("How close (m, horizontally) he must get to a piece or bin to reach it.")]
        [SerializeField, Min(0.3f)] private float reach = 1.0f;

        [Tooltip("A piece or bin he can't reach within this time (s) is given up.")]
        [SerializeField, Min(5f)] private float legTimeout = 30f;

        private static readonly RaycastHit[] SupportHits = new RaycastHit[8];

        private NavMeshAgent agent;
        private Vector3 home;
        private Quaternion homeRotation;
        private State state;
        private bool stopRequested;
        private float legStarted;
        private TimberBoard target;
        private OffcutBin targetBin;
        private TimberBoard carried;
        private readonly HashSet<TimberBoard> skipped = new HashSet<TimberBoard>();
        private string message;
        private float messageUntil;
        private int stored;

        public string Description => Loc.T("Apprentice", "Çırak");

        public bool IsWorking => state != State.Idle && state != State.Home;

        public string InteractionPrompt
        {
            get
            {
                string text = IsWorking
                    ? Loc.T($"Apprentice: tidying up ({stored} put away) - stop", $"Çırak: toparlıyor ({stored} parça kaldırdı) - dur")
                    : Loc.T("Apprentice: tidy up the offcuts", "Çırak: artıkları toparla");
                return message != null && Time.unscaledTime < messageUntil ? $"{text}\n{message}" : text;
            }
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            home = transform.position;
            homeRotation = transform.rotation;
        }

        private void Start()
        {
            // The walkable area is built in RuntimeNavMesh.Awake; the agent stays off until then (no warning spam).
            if (RuntimeNavMesh.IsReady && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"[{nameof(Apprentice)}] No walkable area here: he will stay put.", this);
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!agent.enabled)
            {
                Say(Loc.T("He can't move from here", "Buradan hareket edemiyor"));
                return;
            }

            if (IsWorking)
            {
                stopRequested = true;
                Say(Loc.T("Stopping after this piece", "Elindekini bırakıp duruyor"));
                if (carried == null)
                {
                    GoHome();
                }

                return;
            }

            skipped.Clear();
            stored = 0;
            stopRequested = false;
            if (!NextPiece())
            {
                Say(Loc.T("No offcuts to tidy up", "Toparlanacak artık yok"));
            }
        }

        private void Update()
        {
            if (!agent.enabled)
            {
                return;
            }

            switch (state)
            {
                case State.ToPiece:
                    UpdateToPiece();
                    break;
                case State.ToBin:
                    UpdateToBin();
                    break;
                case State.Home:
                    if (Arrived(home))
                    {
                        state = State.Idle;
                        transform.rotation = homeRotation;
                    }

                    break;
            }
        }

        private void UpdateToPiece()
        {
            if (!IsOffcut(target, out _))
            {
                // Taken by the player meanwhile, or no longer an offcut.
                NextOrHome();
                return;
            }

            Collider footprint = target.GetComponent<Collider>();
            if (HorizontalDistance(footprint.ClosestPoint(transform.position), transform.position) <= reach)
            {
                PickUp(target);
                return;
            }

            if (TimedOut())
            {
                skipped.Add(target);
                NextOrHome();
            }
        }

        private void UpdateToBin()
        {
            bool there = HorizontalDistance(targetBin.ServicePosition, transform.position) <= reach * 0.6f;
            if (there || TimedOut())
            {
                // Out of time: he still gets it there (it never stays stuck in his hands).
                TimberBoard board = carried;
                carried = null;
                board.transform.SetParent(null, true);
                if (targetBin.Store(board))
                {
                    stored++;
                }

                NextOrHome();
            }
        }

        private void PickUp(TimberBoard board)
        {
            var carryable = board.GetComponent<Carryable>();
            carryable.RequestSupportCheckAbove();
            carryable.SetCarried(true);
            board.transform.SetParent(carryPoint, false);
            board.transform.localRotation = Quaternion.identity;
            board.transform.localPosition = -carryable.Center;
            carried = board;
            target = null;
            BeginLeg(State.ToBin, targetBin.ServicePosition);
        }

        private void NextOrHome()
        {
            if (stopRequested || !NextPiece())
            {
                GoHome();
            }
        }

        /// <summary>Heads for the nearest offcut that has a bin; false if there is none.</summary>
        private bool NextPiece()
        {
            TimberBoard best = null;
            OffcutBin bestBin = null;
            float bestDistance = float.MaxValue;
            foreach (TimberBoard board in FindObjectsByType<TimberBoard>(FindObjectsSortMode.None))
            {
                if (skipped.Contains(board) || !IsOffcut(board, out OffcutBin bin))
                {
                    continue;
                }

                float distance = (board.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = board;
                    bestBin = bin;
                    bestDistance = distance;
                }
            }

            if (best == null)
            {
                return false;
            }

            Collider footprint = best.GetComponent<Collider>();
            if (!NavMesh.SamplePosition(footprint.ClosestPoint(transform.position), out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                skipped.Add(best);
                return NextPiece();
            }

            target = best;
            targetBin = bestBin;
            BeginLeg(State.ToPiece, hit.position);
            return true;
        }

        /// <summary>Whether the board is a loose offcut he should put away, and the bin for it.</summary>
        private bool IsOffcut(TimberBoard board, out OffcutBin bin)
        {
            bin = null;
            if (board == null || board.Piece == null || !board.Piece.Species.IsTimber || board.Marks.Count > 0 ||
                !board.TryGetComponent(out Carryable carryable) || carryable.IsCarried || carryable.InstalledIn != null)
            {
                return false;
            }

            foreach (OffcutBin candidate in OffcutBin.All)
            {
                if (candidate.Accepts(board))
                {
                    bin = candidate;
                    break;
                }
            }

            return bin != null && !IsStockSize(board.Piece) && (jobBoard == null || !jobBoard.NeedsPiece(board.Piece)) &&
                   !OnStoragePlace(board);
        }

        /// <summary>A full board as sold (it belongs on the rack or is the job's stock), not an offcut.</summary>
        private bool IsStockSize(TimberPiece piece)
        {
            if (economy == null || economy.Catalog == null)
            {
                return false;
            }

            foreach (CatalogItem item in economy.Catalog.Items)
            {
                if (item.Kind == CatalogItemKind.Board && item.BoardStock.SpeciesId == piece.Species.Id &&
                    item.BoardStock.Dimensions == piece.Dimensions)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OnStoragePlace(TimberBoard board)
        {
            Vector3 center = board.GetComponent<Collider>().bounds.center;
            int count = Physics.RaycastNonAlloc(center, Vector3.down, SupportHits, 1f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (SupportHits[i].collider.GetComponentInParent<KeepPiecesHere>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Before saving: a piece in his hands goes into its bin at once, and he is back at his place.</summary>
        public void FinishNow()
        {
            if (carried != null && targetBin != null)
            {
                TimberBoard board = carried;
                carried = null;
                board.transform.SetParent(null, true);
                targetBin.Store(board);
            }

            ResetToHome();
        }

        /// <summary>After loading: idle at his place (the pieces he was after may be gone).</summary>
        public void ResetToHome()
        {
            target = null;
            targetBin = null;
            state = State.Idle;
            if (agent.enabled)
            {
                agent.ResetPath();
                agent.Warp(home);
            }

            transform.rotation = homeRotation;
        }

        private void GoHome()
        {
            target = null;
            BeginLeg(State.Home, home);
        }

        private void BeginLeg(State next, Vector3 destination)
        {
            state = next;
            legStarted = Time.time;
            agent.SetDestination(destination);
        }

        private bool TimedOut() =>
            Time.time - legStarted > legTimeout || (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid);

        private bool Arrived(Vector3 destination) =>
            !agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance + 0.05f ||
                                   HorizontalDistance(destination, transform.position) < 0.2f || TimedOut());

        private void Say(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 3f;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
