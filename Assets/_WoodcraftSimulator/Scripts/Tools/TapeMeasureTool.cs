using System;
using UnityEngine;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// Tape measure in hand: aim at a board and read the distance from the hooked end, in 1 mm steps.
    /// The hook goes onto the board end nearest to the player when the board is first aimed at.
    /// The extended blade is drawn along the board's top face with a LineRenderer (child object, because a
    /// GameObject can't have both a MeshRenderer and a LineRenderer).
    /// All measuring rules live in the domain (<see cref="TapeMeasure"/>); this class only converts to/from 3D.
    /// </summary>
    public sealed class TapeMeasureTool : MonoBehaviour, IHeldTool
    {
        [SerializeField] private LineRenderer blade;

        [Tooltip("Maximum measuring distance from the eyes, in meters.")]
        [SerializeField, Min(0.1f)] private float range = 2.5f;

        [Tooltip("Graduation of the tape in millimeters.")]
        [SerializeField, Min(0.1f)] private float resolutionMillimeters = 1f;

        [SerializeField] private LayerMask measureMask = Physics.DefaultRaycastLayers;

        // Blade drawn slightly above the surface to avoid z-fighting.
        private const float BladeSurfaceOffset = 0.001f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private HeldToolContext context;
        private Length resolution;

        private TimberBoard hookedBoard;
        private BoardEnd hookedEnd;
        private Length lastReading;
        private Length lastBoardLength;
        private bool hasReading;

        public string Readout { get; private set; }

        public event Action ReadoutChanged;

        /// <summary>The current reading, valid while <see cref="HasReading"/> is true. Used by the pencil (3c).</summary>
        public Length Reading => lastReading;

        public bool HasReading => hasReading;

        private void Awake()
        {
            resolution = Length.FromMillimeters((double)resolutionMillimeters);
            SetBladeVisible(false);
        }

        public void Equip(HeldToolContext toolContext) => context = toolContext;

        public void Unequip()
        {
            context = null;
            hookedBoard = null;
            ClearReading();
        }

        private void Update()
        {
            // Also runs while lying on the bench (active GameObject); only measure when held.
            if (context == null)
            {
                return;
            }

            Transform eyes = context.Eyes;
            var ray = new Ray(eyes.position, eyes.forward);

            if (!PhysicsQueries.RaycastIgnoring(ray, range, measureMask, context.PlayerRoot, hitBuffer, out RaycastHit hit)
                || !hit.collider.TryGetComponent(out TimberBoard board) || board.Piece == null)
            {
                hookedBoard = null;
                ClearReading();
                return;
            }

            Length boardLength = board.Piece.Dimensions.Length;
            float halfLength = (float)(boardLength.Meters / 2.0);

            if (board != hookedBoard)
            {
                // Hook onto the end closest to the player (player position projected onto the board axis).
                float playerZ = board.transform.InverseTransformPoint(context.PlayerRoot.position).z;
                hookedBoard = board;
                hookedEnd = TapeMeasure.NearestEnd(boardLength, Length.FromMeters(playerZ + halfLength));
            }

            Vector3 localPoint = board.transform.InverseTransformPoint(hit.point);
            Length pointFromStart = Length.FromMeters(localPoint.z + halfLength);
            Length reading = TapeMeasure.Read(boardLength, pointFromStart, hookedEnd, resolution);

            UpdateBlade(board, halfLength, localPoint.z);
            SetReading(reading, boardLength);
        }

        private void UpdateBlade(TimberBoard board, float halfLength, float pointZ)
        {
            float topY = (float)(board.Piece.Dimensions.Thickness.Meters / 2.0) + BladeSurfaceOffset;
            float hookZ = hookedEnd == BoardEnd.Start ? -halfLength : halfLength;
            float endZ = Mathf.Clamp(pointZ, -halfLength, halfLength);

            blade.SetPosition(0, board.transform.TransformPoint(new Vector3(0f, topY, hookZ)));
            blade.SetPosition(1, board.transform.TransformPoint(new Vector3(0f, topY, endZ)));
            SetBladeVisible(true);
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
            SetBladeVisible(false);
            if (!hasReading && Readout == null)
            {
                return;
            }

            hasReading = false;
            Readout = null;
            ReadoutChanged?.Invoke();
        }

        private void SetBladeVisible(bool visible)
        {
            if (blade != null && blade.enabled != visible)
            {
                blade.enabled = visible;
            }
        }
    }
}
