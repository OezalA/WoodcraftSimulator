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
    /// Placement is only possible on a near-horizontal surface within reach and without overlapping anything.
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

        [Tooltip("Steepest surface (degrees) objects can be placed on.")]
        [SerializeField, Range(0f, 89f)] private float maxSurfaceSlope = 30f;

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
            carryRotation = Quaternion.Inverse(YawRotation()) * carryable.transform.rotation;
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
            float minNormalY = Mathf.Cos(maxSurfaceSlope * Mathf.Deg2Rad);

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

            if (!hasHit || hit.normal.y < minNormalY)
            {
                preview.SetActive(false);
                SetStatus(PlacementStatus.NoSurface, null);
                return;
            }

            Vector3 halfExtents = carried.Size * 0.5f;
            float halfHeight = VerticalHalfExtent(rotation, halfExtents);
            Vector3 boxCenter = hit.point + Vector3.up * (halfHeight + SurfaceGap);

            Vector3 checkExtents = Vector3.Max(halfExtents - Vector3.one * OverlapTolerance, Vector3.one * 0.0005f);
            // The carried object's colliders are disabled, so only other objects (and the player) can block.
            // Non-alloc query into a reused buffer: no garbage per frame.
            int overlapCount = Physics.OverlapBoxNonAlloc(boxCenter, checkExtents, overlapBuffer, rotation,
                placementMask, QueryTriggerInteraction.Ignore);
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

        /// <summary>Half of the rotated box's extent along world up.</summary>
        private static float VerticalHalfExtent(Quaternion rotation, Vector3 halfExtents)
        {
            return Mathf.Abs((rotation * Vector3.right).y) * halfExtents.x
                 + Mathf.Abs((rotation * Vector3.up).y) * halfExtents.y
                 + Mathf.Abs((rotation * Vector3.forward).y) * halfExtents.z;
        }
    }
}
