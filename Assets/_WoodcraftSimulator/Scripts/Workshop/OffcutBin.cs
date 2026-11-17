using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// A bin for the offcuts of one species (D-052). The <see cref="Apprentice"/> puts loose offcuts in; they are kept
    /// as data (<see cref="OffcutStock"/>), the pile inside grows with the count. Looking at the bin, the wheel chooses
    /// a size from its list (shown by the HUD's bin panel) and E takes one piece out: it becomes a board in the hands.
    /// The pieces count as material the workshop has (cut list "ready", inventory).
    /// </summary>
    public sealed class OffcutBin : MonoBehaviour, IScrollable, IDescribable, IInteractionHint
    {
        private static readonly List<OffcutBin> AllBins = new List<OffcutBin>();

        [SerializeField] private WoodSpeciesDefinition species;
        [SerializeField] private TimberBoardFactory boardFactory;

        [Tooltip("The visual pile inside, scaled up with the number of pieces.")]
        [SerializeField] private Transform pile;

        [SerializeField, Min(0.01f)] private float pileFullHeight = 0.4f;
        [SerializeField, Min(1)] private int piecesWhenFull = 12;

        [Tooltip("Where the apprentice stands to put a piece in (in front of the bin).")]
        [SerializeField] private Transform servicePoint;

        private int selected;

        /// <summary>All enabled bins.</summary>
        public static IReadOnlyList<OffcutBin> All => AllBins;

        public OffcutStock Stock { get; private set; }

        public WoodSpeciesDefinition Species => species;

        /// <summary>The group (size) chosen with the wheel, as an index into <see cref="OffcutStock.Groups"/>.</summary>
        public int Selected => selected;

        public Vector3 ServicePosition => servicePoint != null ? servicePoint.position : transform.position;

        /// <summary>Raised when the contents or the selection change.</summary>
        public event Action Changed;

        public string Description => Loc.T($"Offcut bin: {SpeciesName}", $"Artık kovası: {SpeciesName}");

        public string InteractionPrompt
        {
            get
            {
                IReadOnlyList<OffcutGroup> groups = Stock.Groups();
                if (groups.Count == 0)
                {
                    return Loc.T($"{Description} (empty)", $"{Description} (boş)");
                }

                OffcutGroup group = groups[Mathf.Clamp(selected, 0, groups.Count - 1)];
                return Loc.T($"Take {SpeciesName} {Describe(group)}", $"{SpeciesName} {Describe(group)} al");
            }
        }

        public string InteractionHint => Stock.Groups().Count > 1
            ? Loc.T("[Wheel]  choose a size (list on the right)", "[Tekerlek]  ölçü seç (liste sağda)")
            : null;

        private string SpeciesName => species != null ? species.DisplayName : name;

        private void Awake()
        {
            boardFactory = Core.Services.Resolve(boardFactory); // in the core scene (D-056)
            Stock = new OffcutStock(species != null ? species.Id : "unknown");
            Stock.Changed += OnStockChanged;
        }

        private void OnEnable() => AllBins.Add(this);

        private void OnDisable() => AllBins.Remove(this);

        private void Start() => UpdatePile();

        /// <summary>Whether this bin is for the board's species.</summary>
        public bool Accepts(TimberBoard board) => board != null && Stock.Accepts(board.Piece);

        /// <summary>Puts a loose board in: it is kept as data and the object goes. False if it isn't this species.</summary>
        public bool Store(TimberBoard board)
        {
            if (!Accepts(board) || !Stock.TryAdd(board.Piece, board.Surface))
            {
                return false;
            }

            Destroy(board.gameObject);
            return true;
        }

        public void Scroll(int steps)
        {
            int count = Stock.Groups().Count;
            if (count == 0)
            {
                return;
            }

            // Wheel up = up the list (towards the longest pieces).
            selected = ((selected - steps) % count + count) % count;
            Changed?.Invoke();
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (boardFactory == null || !interactor.TryGetComponent(out PlayerCarrier carrier) || carrier.IsCarrying ||
                !Stock.TryTake(Mathf.Clamp(selected, 0, Math.Max(0, Stock.Groups().Count - 1)), out StoredPiece taken))
            {
                return;
            }

            // The piece comes out on top of the bin and straight into the hands.
            Vector3 top = transform.position + Vector3.up * (pileFullHeight + 0.25f);
            TimberBoard board = boardFactory.Create(Guid.NewGuid().ToString("N"), species, taken.Piece,
                new BoardMarks(taken.Piece.Dimensions.Length), top, transform.rotation);
            board.SetSurface(taken.Surface);
            carrier.TryPickUp(board.GetComponent<Carryable>());
        }

        /// <summary>A group as "450 × 145 × 18 mm" (+ surface if treated).</summary>
        public static string Describe(OffcutGroup group)
        {
            string surface = group.Surface switch
            {
                BoardSurface.Sanded => Loc.T(", sanded", ", zımparalı"),
                BoardSurface.Oiled => Loc.T(", oiled", ", yağlı"),
                _ => string.Empty
            };
            return group.Dimensions + surface;
        }

        private void OnStockChanged()
        {
            int count = Stock.Groups().Count;
            selected = count == 0 ? 0 : Mathf.Clamp(selected, 0, count - 1);
            UpdatePile();
            Changed?.Invoke();
        }

        private void UpdatePile()
        {
            if (pile == null)
            {
                return;
            }

            float share = Mathf.Clamp01((float)Stock.Count / piecesWhenFull);
            pile.gameObject.SetActive(Stock.Count > 0);
            float height = Mathf.Max(0.02f, share * pileFullHeight);
            Vector3 scale = pile.localScale;
            pile.localScale = new Vector3(scale.x, height, scale.z);
            Vector3 position = pile.localPosition;
            pile.localPosition = new Vector3(position.x, 0.03f + height / 2f, position.z);
        }
    }
}
