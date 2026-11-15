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
using WoodcraftSimulator.Player;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// Hand saw in hand (D-038). The blade goes where you aim (free placement; the wheel moves it in 0.5 mm steps),
    /// not onto the pencil line: the cut is the domain <see cref="Crosscut.TryCutAt"/>, so its accuracy is yours.
    /// Hold Use (LMB) and move the mouse forward/back to saw (<see cref="SawingProcess"/>); meanwhile looking and walking
    /// are locked. While starting, moving the mouse sideways (or fast strokes) makes the blade wander. Releasing pauses
    /// the cut; it continues in its kerf as long as you stay on the same board.
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

        [Tooltip("Line across the board showing where the blade is (the kerf).")]
        [SerializeField] private LineRenderer cutGuide;

        [Tooltip("A pencil line within this distance (mm) of the blade is the line being cut (reference for the result).")]
        [SerializeField, Min(1f)] private float lineSearchMillimeters = 30f;

        [Tooltip("Assist: show the length the measured piece would get if cut at the blade's position.")]
        [SerializeField] private bool showCutPreview = true;

        [Header("Placing the blade (mouse wheel)")]
        [SerializeField, Min(0.1f)] private float nudgeMillimeters = 0.5f;
        [SerializeField, Min(1)] private int coarseSteps = 10;

        [Tooltip("Moving the aim this far (mm) from where the wheel was used gives the blade back to the mouse.")]
        [SerializeField, Min(0.5f)] private float takeoverMillimeters = 3f;

        [Header("Strokes (mouse)")]
        [Tooltip("Saw travel per pixel of forward/back mouse movement.")]
        [SerializeField, Min(0.01f)] private float strokeMillimetersPerPixel = 1.5f;

        [Tooltip("Blade movement along the board per pixel of sideways mouse movement while starting.")]
        [SerializeField, Min(0f)] private float sidewaysMillimetersPerPixel = 0.02f;

        [Header("Stroke animation (camera space)")]
        [SerializeField, Min(0f)] private float strokeLength = 0.08f;

        [SerializeField] private BoardAim aim = new BoardAim();

        private const float GuideSurfaceOffset = 0.0015f;

        private InputAction useAction;
        private InputAction lookAction;
        private InputAction adjustAction;
        private InputAction coarseAction;
        private string useKeyLabel;
        private Saw saw;
        private SawingSettings sawingSettings;
        private PickupItem pickup;
        private HeldToolContext context;
        private PlayerController playerController;
        private int ignoreInputFrame = -1;

        // Free placement (wheel nudge relative to an anchor).
        private bool nudging;
        private Length nudgeAnchor;
        private Length nudge;

        // The cut in progress: its board, where the blade entered and the strokes so far.
        private TimberBoard cutBoard;
        private Length cutStart;
        private SawingProcess process;
        private bool sawing;
        private float cutFaceSign = 1f;

        // Preview of the cut at the current blade position (re-evaluated only when the position changes).
        private TimberBoard previewBoard;
        private Length previewBlade;
        private PencilMark previewMark;
        private CrosscutRefusal previewRefusal;
        private CrosscutResult previewResult;

        private string statusLine;
        private string resultLine;

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
            sawingSettings = definition.SawingSettings();
            useAction = InputSystem.actions.FindAction("Player/UseTool", throwIfNotFound: true);
            lookAction = InputSystem.actions.FindAction("Player/Look", throwIfNotFound: true);
            adjustAction = InputSystem.actions.FindAction("Player/AdjustTool", throwIfNotFound: true);
            coarseAction = InputSystem.actions.FindAction("Player/CoarseAdjust", throwIfNotFound: true);
            useKeyLabel = useAction.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
            SetGuideVisible(false);
        }

        public void Equip(HeldToolContext toolContext)
        {
            context = toolContext;
            playerController = toolContext.PlayerRoot.GetComponent<PlayerController>();
            // The click that resumes from the pause menu must not start sawing.
            ignoreInputFrame = Time.frameCount;
        }

        public void Unequip()
        {
            StopSawing();
            context = null;
            aim.Release();
            AbandonCut();
            ClearPreview();
            nudging = false;
            SetGuideVisible(false);
            resultLine = null;
            SetLines(null, null);
        }

        private void OnDisable() => StopSawing();

        private void Update()
        {
            // Also runs while lying on the bench (active GameObject); only work when held.
            if (context == null)
            {
                return;
            }

            if (sawing)
            {
                ContinueSawing();
                return;
            }

            if (!aim.TryAim(context, out BoardAimPoint point) || !point.IsOnBoard)
            {
                bool onBoard = point.Board != null;
                AbandonCut();
                ClearPreview();
                SetGuideVisible(false);
                SetLines(onBoard ? "Aim at the board where you want to cut" : null, resultLine);
                return;
            }

            TimberBoard board = point.Board;
            if (cutBoard != null && board != cutBoard)
            {
                AbandonCut();
            }

            // A part installed in an assembly would leave its slot holding a piece that no longer exists.
            if (board.TryGetComponent(out Carryable carryable) && carryable.InstalledIn != null)
            {
                ClearPreview();
                SetGuideVisible(false);
                SetLines($"Take it out of the {carryable.InstalledIn.DisplayName} to cut it", resultLine);
                return;
            }

            // A started cut keeps its kerf; otherwise the blade follows the aim (and the wheel).
            Length blade = process != null ? BladePosition() : FreeBladePosition(point.PointFromStart);
            UpdatePreview(board, blade);
            ShowGuide(board, blade, point.FaceSign);

            if (previewRefusal != CrosscutRefusal.None)
            {
                SetLines(RefusalText(previewRefusal), resultLine);
                return;
            }

            if (useAction.IsPressed() && Time.frameCount != ignoreInputFrame)
            {
                StartSawing(board, blade, point.FaceSign);
                return;
            }

            string hint = process != null
                ? $"Cut {Percent(process.Progress)}: hold {useKeyLabel} and move the mouse forward/back to continue"
                : $"Hold {useKeyLabel} and move the mouse forward/back to saw      Wheel: ±{nudgeMillimeters.ToString("0.#", CultureInfo.InvariantCulture)} mm";
            SetLines(PreviewText() + "\n" + hint, resultLine);
        }

        private Length FreeBladePosition(Length aimed)
        {
            if (adjustAction.WasPerformedThisFrame())
            {
                float value = adjustAction.ReadValue<float>();
                if (value != 0f)
                {
                    if (!nudging)
                    {
                        nudging = true;
                        nudgeAnchor = aimed.RoundTo(Length.FromMillimeters(0.5));
                        nudge = Length.Zero;
                    }

                    int steps = Math.Sign(value) * (coarseAction.IsPressed() ? coarseSteps : 1);
                    nudge += Length.FromMillimeters((double)nudgeMillimeters) * steps;
                }
            }

            if (nudging && (aimed - nudgeAnchor).Abs() > Length.FromMillimeters((double)takeoverMillimeters))
            {
                nudging = false;
            }

            return nudging ? nudgeAnchor + nudge : aimed;
        }

        /// <summary>Where the blade is in the cut in progress: where it entered plus how far it wandered.</summary>
        private Length BladePosition() => cutStart + Length.FromMillimeters(process.DriftMillimeters);

        private void StartSawing(TimberBoard board, Length blade, float faceSign)
        {
            if (process == null)
            {
                BoardDimensions dimensions = board.Piece.Dimensions;
                double crossSection = dimensions.Width.Centimeters * dimensions.Thickness.Centimeters;
                process = new SawingProcess(crossSection, board.Piece.Species.DensityKgPerCubicMeter, sawingSettings);
                cutBoard = board;
                cutStart = blade;
                cutFaceSign = faceSign;
            }

            sawing = true;
            resultLine = null;
            if (playerController != null)
            {
                playerController.ControlsLocked = true;
            }
        }

        private void ContinueSawing()
        {
            if (!useAction.IsPressed() || cutBoard == null)
            {
                StopSawing();
                return;
            }

            float seconds = Time.deltaTime;
            if (seconds > 0f)
            {
                Vector2 mouse = lookAction.ReadValue<Vector2>();
                process.Feed(mouse.y * strokeMillimetersPerPixel, mouse.x * sidewaysMillimetersPerPixel, seconds);
            }

            float half = definition.BladeLengthMillimeters / 2f;
            pickup.HandOffset = Vector3.forward * ((float)process.StrokePosition / half * strokeLength);

            Length blade = BladePosition();
            UpdatePreview(cutBoard, blade);
            ShowGuide(cutBoard, blade, cutFaceSign);

            if (previewRefusal != CrosscutRefusal.None)
            {
                // The blade wandered too close to an end.
                StopSawing();
                AbandonCut();
                SetLines(RefusalText(previewRefusal), resultLine);
                return;
            }

            if (process.IsDone)
            {
                StopSawing();
                FinishCut();
                return;
            }

            string state = process.Jumped
                ? "The saw jumped! Start with slower strokes"
                : process.IsStarting
                    ? "Starting: slow strokes, keep the mouse straight"
                    : "In the kerf: long, even strokes";
            double drift = process.DriftMillimeters;
            string wander = Math.Abs(drift) >= 0.1
                ? $"   (blade wandered {Math.Abs(drift).ToString("0.0", CultureInfo.InvariantCulture)} mm)"
                : string.Empty;
            SetLines($"Sawing {Percent(process.Progress)}   {state}{wander}", resultLine);
        }

        private void StopSawing()
        {
            if (sawing && playerController != null)
            {
                playerController.ControlsLocked = false;
            }

            sawing = false;
            if (pickup != null)
            {
                pickup.HandOffset = Vector3.zero;
            }
        }

        private void AbandonCut()
        {
            cutBoard = null;
            process = null;
        }

        private void UpdatePreview(TimberBoard board, Length blade)
        {
            if (board == previewBoard && blade == previewBlade && (previewResult != null || previewRefusal != CrosscutRefusal.None))
            {
                return;
            }

            previewBoard = board;
            previewBlade = blade;
            previewMark = NearestMark(board, blade);

            // The measured piece is the one the line was measured for; without a line, the shorter one.
            BoardEnd measuredSide = previewMark != null
                ? previewMark.MeasuredFrom
                : blade.Millimeters < board.Piece.Dimensions.Length.Millimeters / 2.0 ? BoardEnd.Start : BoardEnd.End;
            previewRefusal = Crosscut.TryCutAt(board.Piece, board.Marks, blade, measuredSide, saw, out previewResult);
        }

        private void ClearPreview()
        {
            previewBoard = null;
            previewMark = null;
            previewResult = null;
            previewRefusal = CrosscutRefusal.None;
        }

        private PencilMark NearestMark(TimberBoard board, Length blade)
        {
            PencilMark nearest = null;
            Length best = Length.FromMillimeters((double)lineSearchMillimeters);
            foreach (PencilMark candidate in board.Marks.All)
            {
                Length distance = (candidate.PositionFromStart - blade).Abs();
                if (distance <= best)
                {
                    best = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        /// <summary>
        /// What cutting here gives, against the pencil line: a carpenter puts the kerf just beside the line on the
        /// waste side, so the piece keeps the marked length.
        /// </summary>
        private string PreviewText()
        {
            if (previewMark == null)
            {
                return "No pencil line near the blade: cutting by eye";
            }

            if (!showCutPreview || previewResult == null)
            {
                return "Blade at the pencil line";
            }

            Length line = previewMark.DistanceFromReference(previewBoard.Piece.Dimensions.Length);
            Length piece = previewResult.MeasuredPiece.Dimensions.Length;
            Length difference = piece - line;
            string relation = difference == Length.Zero
                ? "kerf right beside the line"
                : difference > Length.Zero
                    ? $"{difference} long: blade on the waste side"
                    : $"{difference.Abs()} short: blade over the line";
            return $"Piece {piece} (line {line}, {relation})";
        }

        private void FinishCut()
        {
            TimberBoard original = cutBoard;
            Length blade = BladePosition();
            AbandonCut();
            UpdatePreview(original, blade);
            CrosscutResult result = previewResult;
            PencilMark line = previewMark;
            ClearPreview();
            if (result == null)
            {
                return;
            }

            Transform originalTransform = original.transform;
            double originalLength = original.Piece.Dimensions.Length.Meters;
            Length lineDistance = line != null ? line.DistanceFromReference(original.Piece.Dimensions.Length) : Length.Zero;

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
            nudging = false;
            SetGuideVisible(false);
            resultLine = FormatResult(result, line != null ? lineDistance : (Length?)null);
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

        private string FormatResult(CrosscutResult result, Length? line)
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
            else if (line.HasValue)
            {
                measuredText += $" (line {line.Value})";
            }

            string text = $"Cut: {measuredText} + {result.OtherPiece.Dimensions.Length}" +
                          $"   (kerf {result.Waste.Kerf}, sawdust {CubicCentimeters(result.Waste.VolumeCubicMeters)} cm³)";
            if (waste != null && waste.Log.CutCount > 1)
            {
                text += $"\nTotal sawdust {CubicCentimeters(waste.Log.TotalVolumeCubicMeters)} cm³ in {waste.Log.CutCount} cuts";
            }

            return text;
        }

        private static string Percent(double share) => $"{Math.Floor(share * 100.0).ToString(CultureInfo.InvariantCulture)}%";

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

        /// <summary>The kerf across the board at the blade's position, on the face turned towards the player.</summary>
        private void ShowGuide(TimberBoard board, Length blade, float faceSign)
        {
            if (cutGuide == null)
            {
                return;
            }

            BoardDimensions dimensions = board.Piece.Dimensions;
            float z = (float)(blade.Meters - dimensions.Length.Meters / 2.0);
            float halfWidth = (float)(dimensions.Width.Meters / 2.0);
            float y = faceSign * ((float)(dimensions.Thickness.Meters / 2.0) + GuideSurfaceOffset);

            Transform boardTransform = board.transform;
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
