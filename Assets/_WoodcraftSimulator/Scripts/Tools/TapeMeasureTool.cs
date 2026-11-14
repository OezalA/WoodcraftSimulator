using System;
using UnityEngine;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// Tape measure in hand: aim at a board and read the distance from the hooked end, in 1 mm steps.
    /// Aiming and the sticky hook are shared with the pencil (<see cref="BoardAim"/>); the extended blade is drawn
    /// with a LineRenderer on a child object (a GameObject can't have both a MeshRenderer and a LineRenderer).
    /// All measuring rules live in the domain (<see cref="TapeMeasure"/>); this class only converts to/from 3D.
    /// </summary>
    public sealed class TapeMeasureTool : MonoBehaviour, IHeldTool
    {
        [SerializeField] private LineRenderer blade;

        [Tooltip("Graduation of the tape in millimeters.")]
        [SerializeField, Min(0.1f)] private float resolutionMillimeters = 1f;

        [SerializeField] private BoardAim aim = new BoardAim();

        private HeldToolContext context;
        private Length resolution;
        private Length lastReading;
        private Length lastBoardLength;
        private bool hasReading;

        public string Readout { get; private set; }

        public event Action ReadoutChanged;

        private void Awake()
        {
            resolution = Length.FromMillimeters((double)resolutionMillimeters);
            TapeBladeView.Hide(blade);
        }

        public void Equip(HeldToolContext toolContext) => context = toolContext;

        public void Unequip()
        {
            context = null;
            aim.Release();
            ClearReading();
        }

        private void Update()
        {
            // Also runs while lying on the bench (active GameObject); only measure when held.
            if (context == null)
            {
                return;
            }

            if (!aim.TryAim(context, out BoardAimPoint point))
            {
                ClearReading();
                return;
            }

            Length reading = TapeMeasure.Read(point.BoardLength, point.PointFromStart, point.HookedEnd, resolution);
            TapeBladeView.Show(blade, point);
            SetReading(reading, point.BoardLength);
        }

        private void SetReading(Length reading, Length boardLength)
        {
            // Only build a new string when the shown text changes (not every frame).
            if (hasReading && reading == lastReading && boardLength == lastBoardLength)
            {
                return;
            }

            lastReading = reading;
            lastBoardLength = boardLength;
            hasReading = true;
            Readout = $"{reading.FormatMillimeters()} mm   (board {boardLength.FormatMillimeters()} mm)";
            ReadoutChanged?.Invoke();
        }

        private void ClearReading()
        {
            TapeBladeView.Hide(blade);
            if (!hasReading && Readout == null)
            {
                return;
            }

            hasReading = false;
            Readout = null;
            ReadoutChanged?.Invoke();
        }
    }
}
