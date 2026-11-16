using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Interaction;

namespace WoodcraftSimulator.Handling
{
    /// <summary>
    /// Lets the player carry one <see cref="Carryable"/> at a time, rotate it, and place it on a surface.
    /// While carrying, a translucent preview shows the target pose: green = valid, red = blocked.
    /// The object is lowered onto the highest support under its footprint near the aimed point (D-047), and placement
    /// is only possible within reach and without overlapping anything; if only the player is in the way, the
    /// placement moves away along the view.
    /// </summary>
    public enum PlacementStatus
    {
        /// <summary>No suitable surface within reach under the crosshair.</summary>
        NoSurface,
        Valid,
        /// <summary>The target pose overlaps another collider (see <see cref="PlayerCarrier.Blocker"/>).</summary>
        Blocked
    }

    public sealed class PlayerCarrier : MonoBehaviour
    {
        // Small vertical gap above the supporting surface, and shrink applied to the overlap test, so that
        // touching the support (or a neighbour) doesn't count as a collision.
        private const float SurfaceGap = 0.002f;
        private const float OverlapTolerance = 0.003f;

        // Lowering onto a support (D-047): the object may rest up to MaxStepUp above the aimed point (sawhorse beams
        // 0.65 m above the floor, plus a board on them), never on something higher (e.g. the workbench next to it).
        private const float MaxStepUp = 0.75f;
        private const float BelowAim = 0.05f;

        // When only the player blocks, the placement moves away along the view in these steps (at most 1.2 m).
        private const float PlayerClearanceStep = 0.1f;
        private const int PlayerClearanceSteps = 12;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color ValidColor = new Color(0.30f, 1.00f, 0.40f, 0.35f);
        private static readonly Color BlockedColor = new Color(1.00f, 0.30f, 0.25f, 0.35f);

        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlayerInteractor interactor;

        [Tooltip("Translucent material for the placement preview (URP Unlit, transparent).")]
        [SerializeField] private Material previewMaterial;

        [Tooltip("Where the carried object is held, relative to the eyes (x right, y up, z forward), in meters.")]
        [SerializeField] private Vector3 holdOffset = new Vector3(0.25f, -0.35f, 0.9f);

        [Tooltip("Maximum distance from the eyes to the placement point, in meters.")]
        [SerializeField, Min(0.5f)] private float placementRange = 3f;

        [SerializeField] private LayerMask placementMask = Physics.DefaultRaycastLayers;

        private InputAction placeAction;
        private InputAction rotateAction;
        private InputAction tiltAction;

        private Carryable carried;
        private Quaternion carryRotation = Quaternion.identity;

        private GameObject preview;
        private MeshRenderer previewRenderer;
        private MaterialPropertyBlock previewBlock;

        private readonly Collider[] overlapBuffer = new Collider[8];
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private readonly RaycastHit[] castBuffer = new RaycastHit[16];
        private RaycastHit lastSurfaceHit;
        private bool lastSurfaceHitValid;
        private int lastOverlapCount;
        private int ignoreInputFrame = -1;
        private bool hasPlacement;
        private Vector3 placementPosition;
        private Quaternion placementRotation;

        // Set while the aim offers a snap into an IPlacementTarget (e.g. an assembly slot) instead of free placement.
        private IPlacementTarget snapTarget;
        private SnapPlacement snap;

        public bool IsCarrying => carried != null;

        public Carryable Carried => carried;

        public PlacementStatus Status { get; private set; } = PlacementStatus.NoSurface;

        /// <summary>First collider blocking the placement while <see cref="Status"/> is Blocked, otherwise null.</summary>
        public Collider Blocker { get; private set; }

        /// <summary>Text from a snap target (why it doesn't fit, or a note on an allowed snap), otherwise null.</summary>
        public string StatusMessage { get; private set; }

        /// <summary>Raised with true on pick-up and false after placing.</summary>
        public event Action<bool> CarryingChanged;

        /// <summary>Raised only when <see cref="Status"/> or <see cref="Blocker"/> changes (not every frame).</summary>
        public event Action PlacementStatusChanged;

        private void Awake()
        {
            if (cameraTransform == null || interactor == null || previewMaterial == null)
            {
                Debug.LogError($"{nameof(PlayerCarrier)}: Camera Transform, Interactor and Preview Material must be assigned.", this);
                enabled = false;
                return;
            }

            InputActionAsset actions = InputSystem.actions;
            placeAction = actions.FindAction("Player/Place", throwIfNotFound: true);
            rotateAction = actions.FindAction("Player/Rotate", throwIfNotFound: true);
            tiltAction = actions.FindAction("Player/Tilt", throwIfNotFound: true);
            previewBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            // A click that resumes the game (pause menu) must not also place the carried object.
            ignoreInputFrame = Time.frameCount;
        }

        private void OnDisable()
        {
            // Paused: hide the preview; the carried object simply stays where it is until re-enabled.
            if (preview != null)
            {
                preview.SetActive(false);
            }
        }

        public bool TryPickUp(Carryable carryable)
        {
            if (!enabled || IsCarrying || carryable == null)
            {
                return false;
            }

            // Whatever lies on it loses its support: those objects check it at the next physics step (D-031).
            carryable.RequestSupportCheckAbove();

            carried = carryable;
            // Held level: a piece that fell and lies tilted or upright (e.g. an offcut half on the sawhorse, half on
            // the ground) comes up with its length horizontal, flat or on edge (D-047).
            carryRotation = LevelForCarrying(Quaternion.Inverse(YawRotation()) * carryable.transform.rotation);
            carried.SetCarried(true);
            CreatePreview();

            // Hands are full: no other interactions until the object is placed.
            interactor.IsBlocked = true;
            CarryingChanged?.Invoke(true);
            return true;
        }

        // LateUpdate: runs after PlayerController.Update has turned the camera this frame.
        private void LateUpdate()
        {
            if (!IsCarrying)
            {
                return;
            }

            if (rotateAction.WasPressedThisFrame())
            {
                // Quarter turn around the vertical axis.
                carryRotation = Quaternion.Euler(0f, 90f, 0f) * carryRotation;
            }

            if (tiltAction.WasPressedThisFrame())
            {
                // Quarter turn around the object's own length axis (e.g. a board flat ↔ on edge).
                carryRotation *= Quaternion.Euler(0f, 0f, 90f);
            }

            Quaternion rotation = YawRotation() * carryRotation;
            carried.transform.SetPositionAndRotation(
                cameraTransform.position + YawRotation() * holdOffset, rotation);

            UpdatePlacement(rotation);

            if (placeAction.WasPressedThisFrame() && Time.frameCount != ignoreInputFrame)
            {
                if (hasPlacement)
                {
                    Place();
                }
                else
                {
                    LogRefusedPlacement();
                }
            }
        }

        private void UpdatePlacement(Quaternion rotation)
        {
            hasPlacement = false;
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);

            // Skip the player's own CharacterController: looking down, the ray reports it at ~0.1 m and the player's
            // head would be taken as the placement surface. The overlap test below still includes the player.
            bool hasHit = PhysicsQueries.RaycastIgnoring(ray, placementRange, placementMask, transform, hitBuffer,
                out RaycastHit hit);
            lastSurfaceHit = hit;
            lastSurfaceHitValid = hasHit;
            snapTarget = null;

            // Predefined places (e.g. assembly slots) win over free placement when the aim is close to one.
            if (hasHit && TrySnap(hit))
            {
                return;
            }

            Vector3 halfExtents = carried.Size * 0.5f;
            float halfHeight = VerticalHalfExtent(rotation, halfExtents);
            Vector3 checkExtents = Vector3.Max(halfExtents - Vector3.one * OverlapTolerance, Vector3.one * 0.0005f);

            // The object is lowered onto the highest support under its footprint (D-047): aiming between two
            // sawhorses (or at the side of one) still lays a board across both.
            if (!hasHit || !TryFindRestingCenter(hit.point, rotation, checkExtents, halfHeight, out Vector3 boxCenter))
            {
                preview.SetActive(false);
                SetStatus(PlacementStatus.NoSurface, null);
                return;
            }

            int overlapCount = CountOverlaps(boxCenter, checkExtents, rotation);
            if (overlapCount > 0 && OnlyPlayerOverlaps(overlapCount))
            {
                // Only the player is in the way (a long board laid down just in front of you): move the placement
                // away along the view until it is clear.
                Vector3 away = YawRotation() * Vector3.forward;
                for (int step = 1; step <= PlayerClearanceSteps; step++)
                {
                    Vector3 target = hit.point + away * (step * PlayerClearanceStep);
                    if (TryFindRestingCenter(target, rotation, checkExtents, halfHeight, out Vector3 shifted) &&
                        CountOverlaps(shifted, checkExtents, rotation) == 0)
                    {
                        boxCenter = shifted;
                        overlapCount = 0;
                        break;
                    }
                }

                if (overlapCount > 0)
                {
                    // Nothing clear found: report the original pose ("too close to you").
                    overlapCount = CountOverlaps(boxCenter, checkExtents, rotation);
                }
            }

            lastOverlapCount = overlapCount;
            bool blocked = overlapCount > 0;

            placementRotation = rotation;
            placementPosition = boxCenter - rotation * carried.Center;
            hasPlacement = !blocked;
            SetStatus(blocked ? PlacementStatus.Blocked : PlacementStatus.Valid, blocked ? overlapBuffer[0] : null);

            preview.SetActive(true);
            preview.transform.SetPositionAndRotation(placementPosition, placementRotation);
            previewBlock.SetColor(BaseColorId, blocked ? BlockedColor : ValidColor);
            previewRenderer.SetPropertyBlock(previewBlock);
        }

        /// <summary>
        /// Lowers the (rotated) box from <see cref="MaxStepUp"/> above <paramref name="aim"/> straight down and returns
        /// its center resting on the first surface it meets. The player is ignored here (the overlap test catches it);
        /// anything already inside the start box is skipped (also left to the overlap test).
        /// </summary>
        private bool TryFindRestingCenter(Vector3 aim, Quaternion rotation, Vector3 castExtents, float halfHeight,
            out Vector3 center)
        {
            var start = new Vector3(aim.x, aim.y + MaxStepUp + halfHeight, aim.z);
            int count = Physics.BoxCastNonAlloc(start, castExtents, Vector3.down, castBuffer, rotation,
                MaxStepUp + BelowAim, placementMask, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castBuffer[i];
                if (hit.distance > 0f && hit.distance < nearest && !hit.collider.transform.IsChildOf(transform))
                {
                    nearest = hit.distance;
                }
            }

            if (float.IsPositiveInfinity(nearest))
            {
                center = default;
                return false;
            }

            // The cast used the shrunk box: lift by the shrink so the real box rests SurfaceGap above the support.
            center = start + Vector3.down * nearest + Vector3.up * (OverlapTolerance + SurfaceGap);
            return true;
        }

        private int CountOverlaps(Vector3 center, Vector3 extents, Quaternion rotation)
        {
            // The carried object's colliders are disabled, so only other objects (and the player) can block.
            // Non-alloc query into a reused buffer: no garbage per frame.
            return Physics.OverlapBoxNonAlloc(center, extents, overlapBuffer, rotation, placementMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool OnlyPlayerOverlaps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!overlapBuffer[i].transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Shows and selects a snap pose if the aimed object belongs to an <see cref="IPlacementTarget"/>.</summary>
        private bool TrySnap(RaycastHit hit)
        {
            var target = hit.collider.GetComponentInParent<IPlacementTarget>();
            if (target == null || !target.TryGetSnap(carried, hit.point, out SnapPlacement offered))
            {
                return false;
            }

            snapTarget = target;
            snap = offered;
            placementPosition = offered.Position;
            placementRotation = offered.Rotation;
            hasPlacement = offered.Allowed;
            SetStatus(offered.Allowed ? PlacementStatus.Valid : PlacementStatus.Blocked, null, offered.Message);

            preview.SetActive(true);
            preview.transform.SetPositionAndRotation(placementPosition, placementRotation);
            previewBlock.SetColor(BaseColorId, offered.Allowed ? ValidColor : BlockedColor);
            previewRenderer.SetPropertyBlock(previewBlock);
            return true;
        }

        private void Place()
        {
            carried.transform.SetPositionAndRotation(placementPosition, placementRotation);
            carried.SetCarried(false);
            if (snapTarget != null)
            {
                snapTarget.Install(carried, snap);
                snapTarget = null;
            }
            else
            {
                // Its center may be over a gap (across two sawhorses, or hanging over an edge): physics decides (D-031).
                carried.RequestSupportCheck();
            }

            carried = null;
            hasPlacement = false;

            Destroy(preview);
            preview = null;
            SetStatus(PlacementStatus.NoSurface, null);

            interactor.IsBlocked = false;
            CarryingChanged?.Invoke(false);
        }

        /// <summary>
        /// Diagnostic for refused placements (only logged when the player clicks, so no per-frame cost).
        /// Lets placement problems be analysed from the Console / Editor.log.
        /// </summary>
        private void LogRefusedPlacement()
        {
            string surface = lastSurfaceHitValid
                ? $"surface='{lastSurfaceHit.collider.name}' distance={lastSurfaceHit.distance:F2} m " +
                  $"normal={lastSurfaceHit.normal} point={lastSurfaceHit.point}"
                : $"surface=<none within {placementRange:F1} m>";

            // Only the first lastOverlapCount entries are valid: NonAlloc queries don't clear the rest of the buffer.
            string blockers = string.Empty;
            if (Status == PlacementStatus.Blocked)
            {
                for (int i = 0; i < lastOverlapCount; i++)
                {
                    blockers += (i == 0 ? " blockers=" : ", ") + $"'{overlapBuffer[i].name}'";
                }
            }

            Debug.Log($"[{nameof(PlayerCarrier)}] Placement refused: status={Status} carrying='{carried.name}' " +
                      $"size={carried.Size} {surface}{blockers} eyes={cameraTransform.position}", this);
        }

        private void SetStatus(PlacementStatus status, Collider blocker, string message = null)
        {
            if (status == Status && blocker == Blocker && message == StatusMessage)
            {
                return;
            }

            Status = status;
            Blocker = blocker;
            StatusMessage = message;
            PlacementStatusChanged?.Invoke();
        }

        private void CreatePreview()
        {
            preview = new GameObject("PlacementPreview");
            preview.SetActive(false);

            var meshFilter = preview.AddComponent<MeshFilter>();
            if (carried.TryGetComponent(out MeshFilter sourceFilter))
            {
                meshFilter.sharedMesh = sourceFilter.sharedMesh;
            }

            previewRenderer = preview.AddComponent<MeshRenderer>();
            previewRenderer.sharedMaterial = previewMaterial;
            previewRenderer.shadowCastingMode = ShadowCastingMode.Off;
            previewRenderer.receiveShadows = false;
        }

        /// <summary>Camera heading without pitch, so carried objects stay level when looking up or down.</summary>
        private Quaternion YawRotation() => Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);

        /// <summary>
        /// The orientation a picked-up piece is held in (D-047): its length (local Z) always horizontal, turned to the
        /// nearest quarter of the view; lying flat (thickness up) or on edge (width up), whichever it was closer to.
        /// Never standing on its end, however it fell.
        /// </summary>
        private static Quaternion LevelForCarrying(Quaternion rotation)
        {
            Vector3 length = rotation * Vector3.forward;
            var horizontal = new Vector3(length.x, 0f, length.z);
            // Standing on its end: no heading to keep, hold it pointing away.
            Vector3 forward = horizontal.sqrMagnitude > 0.01f ? NearestAxis(horizontal) : Vector3.forward;

            Vector3 thickness = rotation * Vector3.up;
            Vector3 width = rotation * Vector3.right;
            Vector3 up;
            if (Mathf.Abs(thickness.y) >= Mathf.Abs(width.y))
            {
                up = thickness.y >= 0f ? Vector3.up : Vector3.down;
            }
            else
            {
                // On edge: the thickness points sideways, across the length.
                Vector3 across = Vector3.Cross(Vector3.up, forward);
                up = Vector3.Dot(thickness, across) >= 0f ? across : -across;
            }

            return Quaternion.LookRotation(forward, up);
        }

        private static Vector3 NearestAxis(Vector3 v)
        {
            float x = Mathf.Abs(v.x);
            float y = Mathf.Abs(v.y);
            float z = Mathf.Abs(v.z);
            if (x >= y && x >= z)
            {
                return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            }

            return y >= z ? new Vector3(0f, Mathf.Sign(v.y), 0f) : new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        /// <summary>Half of the rotated box's extent along world up.</summary>
        private static float VerticalHalfExtent(Quaternion rotation, Vector3 halfExtents)
        {
            return Mathf.Abs((rotation * Vector3.right).y) * halfExtents.x
                 + Mathf.Abs((rotation * Vector3.up).y) * halfExtents.y
                 + Mathf.Abs((rotation * Vector3.forward).y) * halfExtents.z;
        }
    }
}
