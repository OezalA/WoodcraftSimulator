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

        /// <summary>
        /// While true, the mouse and movement keys don't turn or move the player: a tool uses the mouse instead
        /// (e.g. sawing strokes, D-038). Gravity still applies.
        /// </summary>
        public bool ControlsLocked { get; set; }

        private void Update()
        {
            HandlePrecisionLook();
            if (!ControlsLocked)
            {
                HandleLook();
            }

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
            bool isGrounded = characterController.isGrounded;

            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = GroundedStickVelocity;
            }

            Vector2 input = ControlsLocked ? Vector2.zero : moveAction.ReadValue<Vector2>();
            Vector3 direction = transform.right * input.x + transform.forward * input.y;
            direction = Vector3.ClampMagnitude(direction, 1f);

            float speed = sprintAction.IsPressed() ? sprintSpeed : walkSpeed;

            if (isGrounded && !ControlsLocked && jumpAction.WasPressedThisFrame())
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
