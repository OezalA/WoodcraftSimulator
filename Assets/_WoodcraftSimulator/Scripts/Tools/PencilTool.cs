using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// Carpenter's pencil in hand: aim along a board and press Use (LMB) to draw a line across it.
    /// Measuring needs a tape measure on the belt: it is treated as laid along the board, hooked on the end nearest
    /// to the player (same rules and sticky hook as <see cref="TapeMeasureTool"/>).
    /// The line goes exactly onto the tape graduation shown (as a carpenter ticks at the tape's mark, D-034). Rough
    /// position with the mouse, fine adjustment with the wheel (±1 mm, with Shift ±10 mm); moving the aim clearly away
    /// hands control back to the mouse. The result is checked against the <see cref="CutList"/> target.
    /// </summary>
    public sealed class PencilTool : MonoBehaviour, IHeldTool
    {
        [Tooltip("Tape blade drawn while marking (the tape laid along the board).")]
        [SerializeField] private LineRenderer blade;

        [Tooltip("Preview of the pencil line at the current reading.")]
        [SerializeField] private LineRenderer markPreview;

        [Tooltip("Source of the target dimension. Optional: without it, marks are not checked.")]
        [SerializeField] private CutList cutList;

        // The bench note is in the workshop scene, the tool in the core scene (D-056); null away from the workshop.
        private CutList NoteOnBench => cutList != null ? cutList : cutList = Core.Services.Get<CutList>();

        [Tooltip("Allowed deviation from the target, in millimeters.")]
        [SerializeField, Min(0f)] private float toleranceMillimeters = 1f;

        [Tooltip("Item id of the tape measure that must be on the belt to measure.")]
        [SerializeField] private string tapeMeasureItemId = "tape-measure";

        [Tooltip("Graduation steps per wheel notch with the coarse modifier (Shift).")]
        [SerializeField, Min(1)] private int coarseSteps = 10;

        [Tooltip("After a wheel adjustment, the mouse takes over again once the aim moves this far (mm) along the board.")]
        [SerializeField, Min(1f)] private float mouseTakeoverMillimeters = 20f;

        [SerializeField] private BoardAim aim = new BoardAim();

        private const float PreviewSurfaceOffset = 0.0012f;
        private static string AdjustHint => Loc.T("Wheel: ±1 mm   Shift + wheel: ±10 mm", "Tekerlek: ±1 mm   Shift + tekerlek: ±10 mm");

        private InputAction useAction;
        private InputAction adjustAction;
        private InputAction coarseAction;
        private HeldToolContext context;
        private Tolerance tolerance;
        private Length resolution;
        private int ignoreInputFrame = -1;

        // Wheel adjustment: the reading is held at a fixed graduation on one board / hook end.
        private bool adjusted;
        private TimberBoard adjustedBoard;
        private BoardEnd adjustedEnd;
        private Length adjustedReading;
        private Length aimAtAdjustment;

        // Inputs of the current readout; the string is only rebuilt when one of them changes.
        private string liveLine;
        private string resultLine;
        private Length shownReading;
        private Length shownTarget;
        private bool shownAdjusted;
        private bool shownHasReading;

        public string Readout { get; private set; }

        public event Action ReadoutChanged;

        private void Awake()
        {
            useAction = InputSystem.actions.FindAction("Player/UseTool", throwIfNotFound: true);
            adjustAction = InputSystem.actions.FindAction("Player/AdjustTool", throwIfNotFound: true);
            coarseAction = InputSystem.actions.FindAction("Player/CoarseAdjust", throwIfNotFound: true);
            tolerance = new Tolerance(Length.FromMillimeters((double)toleranceMillimeters));
            resolution = TapeMeasure.DefaultResolution;
            HideLines();
        }

        public void Equip(HeldToolContext toolContext)
        {
            context = toolContext;
            // The click that takes the pencil (or resumes from the pause menu) must not also draw a line.
            ignoreInputFrame = Time.frameCount;
        }

        public void Unequip()
        {
            context = null;
            aim.Release();
            adjusted = false;
            HideLines();
            liveLine = null;
            resultLine = null;
            shownHasReading = false;
            SetReadout(null);
        }

        private void Update()
        {
            // Also runs while lying on the bench (active GameObject); only work when held.
            if (context == null)
            {
                return;
            }

            if (!context.Belt.Contains(tapeMeasureItemId))
            {
                HideLines();
                adjusted = false;
                shownHasReading = false;
                SetLines(Loc.T("Take a tape measure to measure where to mark", "İşaretlenecek yeri ölçmek için metreyi al"), resultLine);
                return;
            }

            if (!aim.TryAim(context, out BoardAimPoint point))
            {
                HideLines();
                adjusted = false;
                shownHasReading = false;
                SetLines(null, resultLine);
                return;
            }

            Length boardLength = point.BoardLength;
            Length aimReading = TapeMeasure.Read(boardLength, point.PointFromStart, point.HookedEnd, resolution);
            Length reading = UpdateAdjustment(point, aimReading);

            // Where the line would go: exactly on the graduation shown.
            Length position = TapeMeasure.PositionFromStart(boardLength, reading, point.HookedEnd);
            float lineZ = (float)(position.Meters - boardLength.Meters / 2.0);
            TapeBladeView.Show(blade, point, lineZ);
            ShowPreview(point, lineZ);
            UpdateLiveLine(reading);

            if (useAction.WasPressedThisFrame() && Time.frameCount != ignoreInputFrame)
            {
                Mark(point, reading, position);
            }
        }

        /// <summary>Applies wheel steps and decides whether the mouse or the adjusted graduation sets the reading.</summary>
        private Length UpdateAdjustment(BoardAimPoint point, Length aimReading)
        {
            if (adjusted && (point.Board != adjustedBoard || point.HookedEnd != adjustedEnd))
            {
                adjusted = false;
            }

            // Mouse moved clearly away from where the wheel adjustment started: the mouse takes over again.
            if (adjusted && (aimReading - aimAtAdjustment).Abs() > Length.FromMillimeters((double)mouseTakeoverMillimeters))
            {
                adjusted = false;
            }

            // Performed once per wheel notch event / d-pad press (a held d-pad doesn't repeat every frame).
            if (adjustAction.WasPerformedThisFrame() && !Interaction.WheelFocus.HeldByTarget)
            {
                float value = adjustAction.ReadValue<float>();
                if (value != 0f)
                {
                    int steps = Math.Sign(value) * (coarseAction.IsPressed() ? coarseSteps : 1);
                    Length start = adjusted ? adjustedReading : aimReading;
                    adjustedReading = TapeMeasure.Nudge(point.BoardLength, start, steps, resolution);
                    if (!adjusted)
                    {
                        aimAtAdjustment = aimReading;
                    }

                    adjusted = true;
                    adjustedBoard = point.Board;
                    adjustedEnd = point.HookedEnd;
                }
            }

            return adjusted ? adjustedReading : aimReading;
        }

        private void Mark(BoardAimPoint point, Length reading, Length position)
        {
            // The sticky hook keeps reading past the board's end; a line needs the pencil on the board.
            if (!adjusted && !point.IsOnBoard)
            {
                SetLines(liveLine, Loc.T("Aim at the board to draw a line", "Çizgi çekmek için tahtaya nişan al"));
                return;
            }

            var face = point.FaceSign > 0f ? BoardFace.Top : BoardFace.Bottom;
            var mark = new PencilMark(position, point.HookedEnd, face);
            point.Board.AddMark(mark);

            Length distance = mark.DistanceFromReference(point.BoardLength);
            SetLines(liveLine, NoteOnBench != null
                ? FormatResult(tolerance.Check(NoteOnBench.Target, distance))
                : Loc.T($"Marked at {distance}", $"{distance} noktası işaretlendi"));
        }

        private string FormatResult(ToleranceCheck check)
        {
            Length deviation = check.Deviation;
            string signed = (deviation > Length.Zero ? "+" : string.Empty) + deviation;
            string verdict = check.IsWithin ? Loc.T("OK", "TAMAM") : Loc.T("OUT OF TOLERANCE", "TOLERANS DIŞI");
            return Loc.T($"Marked at {check.Actual}  ({signed})  {verdict} {tolerance}",
                $"{check.Actual} işaretlendi  ({signed})  {verdict} {tolerance}");
        }

        private void ShowPreview(BoardAimPoint point, float lineZ)
        {
            if (markPreview == null)
            {
                return;
            }

            float halfWidth = (float)(point.Board.Piece.Dimensions.Width.Meters / 2.0);
            float y = point.FaceY(PreviewSurfaceOffset);
            Transform boardTransform = point.Board.transform;
            markPreview.SetPosition(0, boardTransform.TransformPoint(new Vector3(-halfWidth, y, lineZ)));
            markPreview.SetPosition(1, boardTransform.TransformPoint(new Vector3(halfWidth, y, lineZ)));
            if (!markPreview.enabled)
            {
                markPreview.enabled = true;
            }
        }

        private void HideLines()
        {
            TapeBladeView.Hide(blade);
            if (markPreview != null && markPreview.enabled)
            {
                markPreview.enabled = false;
            }
        }

        private void UpdateLiveLine(Length reading)
        {
            Length target = NoteOnBench != null ? NoteOnBench.Target : Length.Zero;
            if (shownHasReading && reading == shownReading && target == shownTarget && adjusted == shownAdjusted)
            {
                return;
            }

            shownHasReading = true;
            shownReading = reading;
            shownTarget = target;
            shownAdjusted = adjusted;
            string line = $"{reading.FormatMillimeters()} mm{(adjusted ? Loc.T(" (wheel)", " (tekerlek)") : string.Empty)}";
            if (NoteOnBench != null)
            {
                line += Loc.T($"   target {target.FormatMillimeters()} mm", $"   hedef {target.FormatMillimeters()} mm");
            }

            SetLines(line, resultLine);
        }

        private void SetLines(string live, string result)
        {
            if (live == liveLine && result == resultLine && Readout != null)
            {
                return;
            }

            liveLine = live;
            resultLine = result;

            // Until the first line is drawn, the second line explains the fine adjustment.
            string second = result ?? (shownHasReading ? AdjustHint : null);
            string text = live == null ? second : second == null ? live : live + "\n" + second;
            SetReadout(text);
        }

        private void SetReadout(string text)
        {
            if (text == Readout)
            {
                return;
            }

            Readout = text;
            ReadoutChanged?.Invoke();
        }
    }
}
