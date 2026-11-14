using System;
using UnityEngine;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tools
{
    /// <summary>Where the aim meets a board's length axis, with the tape hooked on one end.</summary>
    public readonly struct BoardAimPoint
    {
        public BoardAimPoint(TimberBoard board, float pointZ, BoardEnd hookedEnd, float faceSign)
        {
            Board = board;
            PointZ = pointZ;
            HookedEnd = hookedEnd;
            FaceSign = faceSign;
        }

        public TimberBoard Board { get; }

        /// <summary>Aimed point along the board's length axis, local meters from its center (may lie beyond the ends).</summary>
        public float PointZ { get; }

        public BoardEnd HookedEnd { get; }

        /// <summary>+1 if the board's local +Y face is turned towards the player, −1 for the −Y face.</summary>
        public float FaceSign { get; }

        public Length BoardLength => Board.Piece.Dimensions.Length;

        public float HalfLength => (float)(BoardLength.Meters / 2.0);

        /// <summary>The aimed point as an exact domain position from the Start end (not clamped).</summary>
        public Length PointFromStart => Length.FromMeters(PointZ + HalfLength);

        /// <summary>False when the sticky hook is holding on beyond one of the board's ends.</summary>
        public bool IsOnBoard => Mathf.Abs(PointZ) <= HalfLength;

        /// <summary>Local height of the face turned towards the player, plus <paramref name="offset"/>.</summary>
        public float FaceY(float offset) => FaceSign * ((float)(Board.Piece.Dimensions.Thickness.Meters / 2.0) + offset);
    }

    /// <summary>
    /// Shared aiming for tools that work along a board (tape measure, pencil): finds the aimed board, hooks the
    /// tape on the end nearest to the player, and keeps the hook while the aim slips slightly off the board or past
    /// its end (sticky hook, D-026). Plain serializable class so each tool shows these settings in its Inspector.
    /// </summary>
    [Serializable]
    public sealed class BoardAim
    {
        [Tooltip("Maximum distance from the eyes, in meters.")]
        [SerializeField, Min(0.1f)] private float range = 2.5f;

        [SerializeField] private LayerMask mask = Physics.DefaultRaycastLayers;

        [Tooltip("Once hooked, keep measuring while the aim passes within this distance (m) of the board's axis.")]
        [SerializeField, Min(0f)] private float stickyDistance = 0.25f;

        [Tooltip("Once hooked, keep measuring up to this far (m) beyond the board's ends.")]
        [SerializeField, Min(0f)] private float stickyOvershoot = 0.6f;

        [NonSerialized] private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        [NonSerialized] private TimberBoard hookedBoard;
        [NonSerialized] private BoardEnd hookedEnd;

        /// <summary>Forgets the hooked board (e.g. when the tool is put away).</summary>
        public void Release() => hookedBoard = null;

        public bool TryAim(HeldToolContext context, out BoardAimPoint point)
        {
            Transform eyes = context.Eyes;
            var ray = new Ray(eyes.position, eyes.forward);

            TimberBoard board;
            float pointZ;

            if (PhysicsQueries.RaycastIgnoring(ray, range, mask, context.PlayerRoot, hitBuffer, out RaycastHit hit)
                && hit.collider.TryGetComponent(out TimberBoard hitBoard) && hitBoard.Piece != null)
            {
                // Aiming directly at a board: hook onto it (if new) and use the hit point.
                board = hitBoard;
                if (board != hookedBoard)
                {
                    Hook(board, context.PlayerRoot);
                }

                pointZ = board.transform.InverseTransformPoint(hit.point).z;
            }
            else if (hookedBoard != null && hookedBoard.Piece != null && TryProjectOntoHookedBoard(ray, out pointZ))
            {
                // Sticky hook: the aim slid off the board (or past its end) but stays close to its axis.
                board = hookedBoard;
            }
            else
            {
                hookedBoard = null;
                point = default;
                return false;
            }

            float faceSign = Vector3.Dot(board.transform.up, eyes.position - board.transform.position) >= 0f ? 1f : -1f;
            point = new BoardAimPoint(board, pointZ, hookedEnd, faceSign);
            return true;
        }

        private void Hook(TimberBoard board, Transform playerRoot)
        {
            // Hook onto the end closest to the player (player position projected onto the board axis).
            Length boardLength = board.Piece.Dimensions.Length;
            float halfLength = (float)(boardLength.Meters / 2.0);
            float playerZ = board.transform.InverseTransformPoint(playerRoot.position).z;
            hookedBoard = board;
            hookedEnd = TapeMeasure.NearestEnd(boardLength, Length.FromMeters(playerZ + halfLength));
        }

        /// <summary>
        /// Closest approach between the aim ray and the hooked board's length axis (a line through its center along
        /// local Z). Succeeds while the ray passes within <see cref="stickyDistance"/> of the axis, in front of the
        /// eyes and within range, and not too far beyond the board's ends.
        /// </summary>
        private bool TryProjectOntoHookedBoard(Ray ray, out float pointZ)
        {
            pointZ = 0f;
            Transform boardTransform = hookedBoard.transform;
            Vector3 axisOrigin = boardTransform.position;
            Vector3 axisDirection = boardTransform.forward;

            // Minimise |(o + t·d) − (a + s·u)| for unit d and u.
            Vector3 w0 = ray.origin - axisOrigin;
            float b = Vector3.Dot(ray.direction, axisDirection);
            float denominator = 1f - b * b;
            if (denominator < 1e-6f)
            {
                return false; // looking straight along the board
            }

            float dw = Vector3.Dot(ray.direction, w0);
            float uw = Vector3.Dot(axisDirection, w0);
            float t = (b * uw - dw) / denominator; // along the ray
            float s = uw + t * b;                  // along the board axis (local Z, transform scale is 1)

            float halfLength = (float)(hookedBoard.Piece.Dimensions.Length.Meters / 2.0);
            Vector3 gap = ray.GetPoint(t) - (axisOrigin + axisDirection * s);

            if (t < 0f || t > range || gap.magnitude > stickyDistance || Mathf.Abs(s) > halfLength + stickyOvershoot)
            {
                return false;
            }

            pointZ = s;
            return true;
        }
    }

    /// <summary>Draws the extended tape blade along the face of the aimed board, from the hook to the aimed point.</summary>
    public static class TapeBladeView
    {
        // Slightly above the surface to avoid z-fighting.
        private const float SurfaceOffset = 0.001f;

        public static void Show(LineRenderer blade, BoardAimPoint point) => Show(blade, point, point.PointZ);

        /// <summary>Blade from the hook to <paramref name="endZ"/> (local Z on the board, clamped to the board).</summary>
        public static void Show(LineRenderer blade, BoardAimPoint point, float endZ)
        {
            if (blade == null)
            {
                return;
            }

            float y = point.FaceY(SurfaceOffset);
            float hookZ = point.HookedEnd == BoardEnd.Start ? -point.HalfLength : point.HalfLength;
            endZ = Mathf.Clamp(endZ, -point.HalfLength, point.HalfLength);
            Transform boardTransform = point.Board.transform;

            blade.SetPosition(0, boardTransform.TransformPoint(new Vector3(0f, y, hookZ)));
            blade.SetPosition(1, boardTransform.TransformPoint(new Vector3(0f, y, endZ)));
            SetVisible(blade, true);
        }

        public static void Hide(LineRenderer blade) => SetVisible(blade, false);

        private static void SetVisible(LineRenderer blade, bool visible)
        {
            if (blade != null && blade.enabled != visible)
            {
                blade.enabled = visible;
            }
        }
    }
}
