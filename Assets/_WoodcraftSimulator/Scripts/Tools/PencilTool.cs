using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// Carpenter's pencil in hand: aim along a board and press Use (LMB) to draw a line across it.
    /// Measuring needs a tape measure on the belt: it is treated as laid along the board, hooked on the end nearest
    /// to the player (same rules and sticky hook as <see cref="TapeMeasureTool"/>), so the live reading shows where
    /// the line will go. The mark is stored exactly; the result is checked against the <see cref="CutList"/> target.
    /// </summary>
    public sealed class PencilTool : MonoBehaviour, IHeldTool
    {
        [Tooltip("Tape blade drawn while marking (the tape laid along the board).")]
        [SerializeField] private LineRenderer blade;

        [Tooltip("Source of the target dimension. Optional: without it, marks are not checked.")]
        [SerializeField] private CutList cutList;

        [Tooltip("Allowed deviation from the target, in millimeters.")]
        [SerializeField, Min(0f)] private float toleranceMillimeters = 1f;

        [Tooltip("Item id of the tape measure that must be on the belt to measure.")]
        [SerializeField] private string tapeMeasureItemId = "tape-measure";

        [SerializeField] private BoardAim aim = new BoardAim();

        private InputAction useAction;
        private HeldToolContext context;
        private Tolerance tolerance;
        private int ignoreInputFrame = -1;

        // Inputs of the current readout; the string is only rebuilt when one of them changes.
        private string liveLine;
        private string resultLine;
        private Length shownReading;
        private Length shownTarget;
        private bool shownHasReading;

        public string Readout { get; private set; }

        public event Action ReadoutChanged;

        private void Awake()
        {
            useAction = InputSystem.actions.FindAction("Player/UseTool", throwIfNotFound: true);
            tolerance = new Tolerance(Length.FromMillimeters((double)toleranceMillimeters));
            TapeBladeView.Hide(blade);
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
            TapeBladeView.Hide(blade);
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
                TapeBladeView.Hide(blade);
                shownHasReading = false;
                SetLines("Take a tape measure to measure where to mark", resultLine);
                return;
            }

            if (!aim.TryAim(context, out BoardAimPoint point))
            {
                TapeBladeView.Hide(blade);
                shownHasReading = false;
                SetLines(null, resultLine);
                return;
            }

            TapeBladeView.Show(blade, point);
            Length reading = TapeMeasure.Read(point.BoardLength, point.PointFromStart, point.HookedEnd,
                TapeMeasure.DefaultResolution);
            UpdateLiveLine(reading);

            if (useAction.WasPressedThisFrame() && Time.frameCount != ignoreInputFrame)
            {
                Mark(point);
            }
        }

        private void Mark(BoardAimPoint point)
        {
            if (!point.IsOnBoard)
            {
                SetLines(liveLine, "Aim at the board to draw a line");
                return;
            }

            // Exact position (0.1 mm), not the rounded tape reading: where the pencil actually is.
            var mark = new PencilMark(point.PointFromStart, point.HookedEnd);
            point.Board.AddMark(mark, point.FaceSign);

            Length distance = mark.DistanceFromReference(point.BoardLength);
            SetLines(liveLine, cutList != null
                ? FormatResult(tolerance.Check(cutList.Target, distance))
                : $"Marked at {distance}");
        }

        private string FormatResult(ToleranceCheck check)
        {
            Length deviation = check.Deviation;
            string signed = (deviation > Length.Zero ? "+" : string.Empty) + deviation;
            string verdict = check.IsWithin ? "OK" : "OUT OF TOLERANCE";
            return $"Marked at {check.Actual}  ({signed})  {verdict} {tolerance}";
        }

        private void UpdateLiveLine(Length reading)
        {
            Length target = cutList != null ? cutList.Target : Length.Zero;
            if (shownHasReading && reading == shownReading && target == shownTarget)
            {
                return;
            }

            shownHasReading = true;
            shownReading = reading;
            shownTarget = target;
            string line = $"{reading.FormatMillimeters()} mm";
            if (cutList != null)
            {
                line += $"   target {target.FormatMillimeters()} mm";
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
            string text = live == null ? result : result == null ? live : live + "\n" + result;
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
