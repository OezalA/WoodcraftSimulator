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

        // Small constant downward speed while grounded so isGrounded stays reliable on slopes/steps.
        private const float GroundedStickVelocity = -2f;

        private CharacterController characterController;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction jumpAction;

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
        }

        private void Update()
        {
            HandleLook();
            HandleMovement();
        }

        private void HandleLook()
        {
            // Mouse delta is already "per frame", so it must NOT be multiplied by Time.deltaTime.
            Vector2 delta = lookAction.ReadValue<Vector2>() * lookSensitivity;

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
