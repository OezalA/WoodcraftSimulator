using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Core;

namespace WoodcraftSimulator.Player
{
    /// <summary>
    /// First-person movement and mouse look built on <see cref="CharacterController"/>.
    /// Reads input from the project-wide Input Actions asset (Project Settings > Input System Package).
    /// The GameObject's origin is at the player's feet; the camera is a child at eye height.
    /// Cursor lock and pausing are handled by <see cref="PauseController"/>, which disables this component while paused.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Child camera transform. Pitch (up/down look) is applied here; yaw rotates the whole body.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Movement (meters / second)")]
        [SerializeField, Min(0f)] private float walkSpeed = 2.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 4.5f;
        [SerializeField, Min(0f)] private float jumpHeight = 0.5f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Look")]
        [SerializeField, Range(0f, 90f)] private float maxPitch = 85f;

        [Header("Precision look (hold Player/PrecisionLook)")]
        [Tooltip("Field of view while looking closely, in degrees. Sensitivity scales with the field of view.")]
        [SerializeField, Range(5f, 70f)] private float precisionFieldOfView = 25f;

        [Tooltip("How fast the field of view changes, in degrees per second.")]
        [SerializeField, Min(1f)] private float fieldOfViewSpeed = 200f;

        [Header("Climbing (D-049)")]
        [Tooltip("How far from the eyes (m) a stepladder can be climbed with Jump.")]
        [SerializeField, Min(0.5f)] private float climbRange = 2f;

        // Small constant downward speed while grounded so isGrounded stays reliable on slopes/steps.
        private const float GroundedStickVelocity = -2f;

        private CharacterController characterController;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction jumpAction;
        private InputAction precisionLookAction;

        private Camera viewCamera;
        private float normalFieldOfView;
        private float pitch;
        private float verticalVelocity;
        private float lookSensitivity;

        private readonly RaycastHit[] climbBuffer = new RaycastHit[16];
        private IClimbable ladder;
        private Vector3 groundPosition;

        /// <summary>Standing on a stepladder (no walking; Jump climbs down).</summary>
        public bool IsOnLadder => ladder != null;

        /// <summary>
        /// Where the player stands on the ground: on a ladder, the spot it was climbed from (saving keeps the player
        /// there, not in the air).
        /// </summary>
        public Vector3 GroundPosition => ladder != null ? groundPosition : transform.position;

        /// <summary>Raised after climbing onto or down from a ladder.</summary>
        public event System.Action LadderChanged;

        /// <summary>Degrees of rotation per pixel of mouse movement. Initialized from <see cref="UserPreferences"/>.</summary>
        public float LookSensitivity
        {
            get => lookSensitivity;
            set => lookSensitivity = Mathf.Clamp(value, UserPreferences.MinMouseSensitivity, UserPreferences.MaxMouseSensitivity);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            LookSensitivity = UserPreferences.MouseSensitivity;

            if (cameraTransform == null)
            {
                Debug.LogError($"{nameof(PlayerController)}: Camera Transform is not assigned.", this);
                enabled = false;
                return;
            }

            // Names follow "<ActionMap>/<Action>" from Assets/InputSystem_Actions.inputactions.
            InputActionAsset actions = InputSystem.actions;
            moveAction = actions.FindAction("Player/Move", throwIfNotFound: true);
            lookAction = actions.FindAction("Player/Look", throwIfNotFound: true);
            sprintAction = actions.FindAction("Player/Sprint", throwIfNotFound: true);
            jumpAction = actions.FindAction("Player/Jump", throwIfNotFound: true);
            precisionLookAction = actions.FindAction("Player/PrecisionLook", throwIfNotFound: true);

            viewCamera = cameraTransform.GetComponent<Camera>();
            normalFieldOfView = viewCamera != null ? viewCamera.fieldOfView : 70f;
        }

        /// <summary>Camera pitch in degrees (positive = looking down).</summary>
        public float Pitch => pitch;

        /// <summary>
        /// Moves the player (e.g. when a save is loaded). The CharacterController is switched off for the move,
        /// because it would otherwise overwrite a directly set position.
        /// </summary>
        public void Teleport(Vector3 position, float yawDegrees, float pitchDegrees)
        {
            LeaveLadder();
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            characterController.enabled = wasEnabled;

            pitch = Mathf.Clamp(pitchDegrees, -maxPitch, maxPitch);
            verticalVelocity = 0f;
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void Update()
        {
            HandlePrecisionLook();
            HandleLook();
            HandleMovement();
        }

        /// <summary>
        /// Holding the button narrows the field of view (like leaning in to read a tape measure) so millimetres
        /// become aimable. While paused this component is disabled, so the zoom simply freezes.
        /// </summary>
        private void HandlePrecisionLook()
        {
            if (viewCamera == null)
            {
                return;
            }

            float target = precisionLookAction.IsPressed() ? precisionFieldOfView : normalFieldOfView;
            viewCamera.fieldOfView = Mathf.MoveTowards(viewCamera.fieldOfView, target, fieldOfViewSpeed * Time.deltaTime);
        }

        private bool TryFindLadder(out IClimbable target)
        {
            target = null;
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (!PhysicsQueries.RaycastIgnoring(ray, climbRange, Physics.DefaultRaycastLayers, transform, climbBuffer,
                    out RaycastHit hit))
            {
                return false;
            }

            target = hit.collider.GetComponentInParent<IClimbable>();
            return target != null && target.CanClimb(out _);
        }

        /// <summary>Gets off the ladder in state only (the caller moves the player); the controller stays as it was.</summary>
        private void LeaveLadder()
        {
            if (ladder == null)
            {
                return;
            }

            ladder.SetOccupied(false);
            ladder = null;
            characterController.enabled = true;
            LadderChanged?.Invoke();
        }

        private void HandleLook()
        {
            // Mouse delta is already "per frame", so it must NOT be multiplied by Time.deltaTime.
            // Scaling by the current field of view keeps the on-screen speed constant while zoomed in.
            float zoomFactor = viewCamera != null ? viewCamera.fieldOfView / normalFieldOfView : 1f;
            Vector2 delta = lookAction.ReadValue<Vector2>() * (lookSensitivity * zoomFactor);

            transform.Rotate(0f, delta.x, 0f, Space.Self);

            pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            // On a stepladder: no walking, no gravity; Jump climbs down to where the player stood (D-049).
            if (ladder != null)
            {
                if (jumpAction.WasPressedThisFrame())
                {
                    Vector3 ground = groundPosition;
                    LeaveLadder();
                    characterController.enabled = false;
                    transform.position = ground;
                    characterController.enabled = true;
                    verticalVelocity = 0f;
                }

                return;
            }

            // Jump while looking at a standing stepladder climbs onto it instead.
            if (jumpAction.WasPressedThisFrame() && TryFindLadder(out IClimbable target))
            {
                groundPosition = transform.position;
                ladder = target;
                ladder.SetOccupied(true);
                characterController.enabled = false;
                transform.position = target.StandPosition;
                verticalVelocity = 0f;
                LadderChanged?.Invoke();
                return;
            }

            bool isGrounded = characterController.isGrounded;

            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = GroundedStickVelocity;
            }

            Vector2 input = moveAction.ReadValue<Vector2>();
            Vector3 direction = transform.right * input.x + transform.forward * input.y;
            direction = Vector3.ClampMagnitude(direction, 1f);

            float speed = sprintAction.IsPressed() ? sprintSpeed : walkSpeed;

            if (isGrounded && jumpAction.WasPressedThisFrame())
            {
                // v = sqrt(2 * g * h)
                verticalVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);
            }

            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = direction * speed + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }
    }
}
