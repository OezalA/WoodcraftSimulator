using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;
using WoodcraftSimulator.Persistence;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// Hand saw in hand: aim at a pencil line on a resting board and hold Use (LMB) to saw through it.
    /// The saw snaps to the line nearest to the crosshair; the cut itself is the domain <see cref="Crosscut"/>
    /// (kerf on the waste side, D-029). When finished, the board is replaced by two boards generated from the result.
    /// Carried boards can't be cut: their colliders are off, and the saw is holstered while carrying anyway.
    /// </summary>
    public sealed class HandSawTool : MonoBehaviour, IHeldTool
    {
        [SerializeField] private SawDefinition definition;

        [Tooltip("Where the sawdust of every cut is recorded.")]
        [SerializeField] private WorkshopWaste waste;

        [Tooltip("Target dimension the measured piece is checked against. Optional.")]
        [SerializeField] private CutList cutList;

        [Tooltip("Allowed deviation of the measured piece from the target, in millimeters.")]
        [SerializeField, Min(0f)] private float toleranceMillimeters = 1f;

        [Tooltip("Line across the board showing where the saw will cut (the kerf).")]
        [SerializeField] private LineRenderer cutGuide;

        [Tooltip("The saw snaps to a pencil line within this distance (mm) of the aimed point along the board.")]
        [SerializeField, Min(1f)] private float snapDistanceMillimeters = 30f;

        [Header("Stroke animation (camera space)")]
        [SerializeField, Min(0f)] private float strokeLength = 0.05f;
        [SerializeField, Min(0.1f)] private float strokesPerSecond = 1.6f;

        [SerializeField] private BoardAim aim = new BoardAim();

        private const float GuideSurfaceOffset = 0.0015f;

        private InputAction useAction;
        private string useKeyLabel;
        private Saw saw;
        private PickupItem pickup;
        private HeldToolContext context;
        private int ignoreInputFrame = -1;

        // Current target and its evaluated cut (re-evaluated only when the target changes).
        private TimberBoard targetBoard;
        private PencilMark targetMark;
        private CrosscutRefusal targetRefusal;
        private CrosscutResult targetResult;
        private float progress;   // 0..1
        private float strokeTime;

        private string statusLine;
        private string resultLine;
        private int shownPercent = -1;

        public string Readout { get; private set; }

        public event Action ReadoutChanged;

        private void Awake()
        {
            pickup = GetComponent<PickupItem>();
            if (definition == null)
            {
                Debug.LogError($"{nameof(HandSawTool)} '{name}': saw definition is not assigned.", this);
                enabled = false;
                return;
            }

            saw = definition.ToDomain();
            useAction = InputSystem.actions.FindAction("Player/UseTool", throwIfNotFound: true);
            useKeyLabel = useAction.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
            SetGuideVisible(false);
        }

        public void Equip(HeldToolContext toolContext)
        {
            context = toolContext;
            // The click that resumes from the pause menu must not start sawing.
            ignoreInputFrame = Time.frameCount;
        }

        public void Unequip()
        {
            context = null;
            aim.Release();
            ClearTarget();
            SetGuideVisible(false);
            resultLine = null;
            SetLines(null, null);
        }

        private void Update()
        {
            // Also runs while lying on the bench (active GameObject); only work when held.
            if (context == null)
            {
                return;
            }

            if (!aim.TryAim(context, out BoardAimPoint point) || !TryFindMark(point, out PencilMark mark))
            {
                bool onBoard = point.Board != null;
                ClearTarget();
                SetGuideVisible(false);
                SetLines(onBoard ? "No pencil line here: mark the cut first" : null, resultLine);
                return;
            }

            if (point.Board != targetBoard || mark != targetMark)
            {
                SetTarget(point.Board, mark);
            }

            ShowGuide(point);

            if (targetRefusal != CrosscutRefusal.None)
            {
                SetLines(RefusalText(targetRefusal), resultLine);
                return;
            }

            // A part installed in an assembly would leave its slot holding a piece that no longer exists.
            if (point.Board.TryGetComponent(out Carryable carryable) && carryable.InstalledIn != null)
            {
                SetLines($"Take it out of the {carryable.InstalledIn.DisplayName} to cut it", resultLine);
                return;
            }

            Length atDistance = mark.DistanceFromReference(point.BoardLength);
            bool sawing = useAction.IsPressed() && Time.frameCount != ignoreInputFrame;
            if (!sawing)
            {
                pickup.HandOffset = Vector3.zero;
                shownPercent = -1;
                SetLines(progress > 0f
                    ? $"Hold {useKeyLabel} to continue sawing  ({Mathf.FloorToInt(progress * 100f)}%)"
                    : $"Hold {useKeyLabel} to saw at {atDistance}", resultLine);
                return;
            }

            BoardDimensions dimensions = point.Board.Piece.Dimensions;
            double crossSectionCm2 = dimensions.Width.Centimeters * dimensions.Thickness.Centimeters;
            progress += Time.deltaTime / definition.SawingSeconds(crossSectionCm2);
            strokeTime += Time.deltaTime;
            pickup.HandOffset = Vector3.forward * (Mathf.Sin(strokeTime * strokesPerSecond * 2f * Mathf.PI) * strokeLength);

            if (progress >= 1f)
            {
                FinishCut();
                return;
            }

            int percent = Mathf.FloorToInt(progress * 100f);
            if (percent != shownPercent)
            {
                shownPercent = percent;
                SetLines($"Sawing  {percent}%", resultLine);
            }
        }

        /// <summary>The pencil line on the aimed board nearest to the aimed point, within the snap distance.</summary>
        private bool TryFindMark(BoardAimPoint point, out PencilMark nearest)
        {
            nearest = null;
            Length aimed = point.PointFromStart;
            Length best = Length.FromMillimeters((double)snapDistanceMillimeters);
            foreach (PencilMark candidate in point.Board.Marks.All)
            {
                Length distance = (candidate.PositionFromStart - aimed).Abs();
                if (distance <= best)
                {
                    best = distance;
                    nearest = candidate;
                }
            }

            return nearest != null;
        }

        private void SetTarget(TimberBoard board, PencilMark mark)
        {
            ClearTarget();
            targetBoard = board;
            targetMark = mark;
            targetRefusal = Crosscut.TryCut(board.Piece, board.Marks, mark, saw, out targetResult);
        }

        private void ClearTarget()
        {
            targetBoard = null;
            targetMark = null;
            targetResult = null;
            targetRefusal = CrosscutRefusal.None;
            progress = 0f;
            strokeTime = 0f;
            shownPercent = -1;
            if (pickup != null)
            {
                pickup.HandOffset = Vector3.zero;
            }
        }

        private void FinishCut()
        {
            TimberBoard original = targetBoard;
            CrosscutResult result = targetResult;
            Transform originalTransform = original.transform;
            double originalLength = original.Piece.Dimensions.Length.Meters;

            // Centers of the two pieces along the original's length axis (local Z from its center).
            float startCenter = (float)(result.KerfStartFromStart.Meters / 2.0 - originalLength / 2.0);
            float endCenter = (float)((result.KerfEndFromStart.Meters + originalLength) / 2.0 - originalLength / 2.0);

            CreatePiece(original, result.StartPiece, result.StartMarks, startCenter);
            CreatePiece(original, result.EndPiece, result.EndMarks, endCenter);

            if (waste != null)
            {
                waste.Log.Record(result.Waste);
            }

            // Whatever lay on the board now rests on the pieces, or on nothing if it was over the kerf.
            if (original.TryGetComponent(out Carryable originalCarryable))
            {
                originalCarryable.RequestSupportCheckAbove();
            }

            Destroy(original.gameObject);
            aim.Release();
            ClearTarget();
            SetGuideVisible(false);
            resultLine = FormatResult(result);
            SetLines(null, resultLine);

            void CreatePiece(TimberBoard source, TimberPiece piece, BoardMarks marks, float centerZ)
            {
                // A copy keeps every component setting of the source (kinematic body, carryable, materials, layer).
                GameObject copy = Instantiate(source.gameObject,
                    originalTransform.TransformPoint(new Vector3(0f, 0f, centerZ)), originalTransform.rotation,
                    originalTransform.parent);
                copy.name = $"{piece.Species.DisplayName}_{piece.Dimensions.Length.FormatMillimeters()}";
                copy.GetComponent<TimberBoard>().SetPiece(piece, marks);

                // A new physical piece: it must not keep the original's save id (D-036).
                if (copy.TryGetComponent(out PersistentId persistentId))
                {
                    persistentId.AssignNew();
                }

                // An offcut sticking out over an edge has nothing under it any more: it falls (D-031).
                if (copy.TryGetComponent(out Carryable carryable))
                {
                    carryable.RequestSupportCheck();
                }
            }
        }

        private string FormatResult(CrosscutResult result)
        {
            Length measured = result.MeasuredPiece.Dimensions.Length;
            string measuredText = measured.ToString();
            if (cutList != null)
            {
                // The cut piece against the job's target: this is what counts, not the pencil line (D-031).
                var tolerance = new Tolerance(Length.FromMillimeters((double)toleranceMillimeters));
                ToleranceCheck check = tolerance.Check(cutList.Target, measured);
                measuredText += $" (target {cutList.Target}, {(check.IsWithin ? "OK" : "OUT OF TOLERANCE")} {tolerance})";
            }

            string text = $"Cut: {measuredText} + {result.OtherPiece.Dimensions.Length}" +
                          $"   (kerf {result.Waste.Kerf}, sawdust {CubicCentimeters(result.Waste.VolumeCubicMeters)} cm³)";
            if (waste != null && waste.Log.CutCount > 1)
            {
                text += $"\nTotal sawdust {CubicCentimeters(waste.Log.TotalVolumeCubicMeters)} cm³ in {waste.Log.CutCount} cuts";
            }

            return text;
        }

        private static string CubicCentimeters(double cubicMeters) =>
            (cubicMeters * 1e6).ToString("0.0", CultureInfo.InvariantCulture);

        private static string RefusalText(CrosscutRefusal refusal)
        {
            switch (refusal)
            {
                case CrosscutRefusal.PieceTooShort:
                    return $"Too close to the end: pieces must be at least {Crosscut.DefaultMinimumPieceLength}";
                case CrosscutRefusal.MarkNotOnPiece:
                    return "This line is not on the board";
                default:
                    return refusal.ToString();
            }
        }

        private void ShowGuide(BoardAimPoint point)
        {
            if (cutGuide == null || targetResult == null && targetRefusal == CrosscutRefusal.None)
            {
                return;
            }

            // Kerf center if the cut is possible, otherwise the pencil line itself.
            BoardDimensions dimensions = point.Board.Piece.Dimensions;
            double fromStart = targetResult != null
                ? (targetResult.KerfStartFromStart.Meters + targetResult.KerfEndFromStart.Meters) / 2.0
                : targetMark.PositionFromStart.Meters;
            float z = (float)(fromStart - dimensions.Length.Meters / 2.0);
            float halfWidth = (float)(dimensions.Width.Meters / 2.0);
            float y = (targetMark.Face == BoardFace.Top ? 1f : -1f) *
                      ((float)(dimensions.Thickness.Meters / 2.0) + GuideSurfaceOffset);

            Transform boardTransform = point.Board.transform;
            cutGuide.SetPosition(0, boardTransform.TransformPoint(new Vector3(-halfWidth, y, z)));
            cutGuide.SetPosition(1, boardTransform.TransformPoint(new Vector3(halfWidth, y, z)));
            SetGuideVisible(true);
        }

        private void SetGuideVisible(bool visible)
        {
            if (cutGuide != null && cutGuide.enabled != visible)
            {
                cutGuide.enabled = visible;
            }
        }

        private void SetLines(string status, string result)
        {
            if (status == statusLine && result == resultLine && Readout != null)
            {
                return;
            }

            statusLine = status;
            resultLine = result;
            string text = status == null ? result : result == null ? status : status + "\n" + result;
            if (text == Readout)
            {
                return;
            }

            Readout = text;
            ReadoutChanged?.Invoke();
        }
    }
}
