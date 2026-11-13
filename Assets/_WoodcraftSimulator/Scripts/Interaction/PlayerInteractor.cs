using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WoodcraftSimulator.Interaction
{
    /// <summary>
    /// Casts a ray from the player's eyes each frame, tracks the focused <see cref="IInteractable"/>
    /// and forwards the Interact input to it. UI listens to <see cref="TargetChanged"/> instead of polling.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Maximum reach from the eyes, in meters.")]
        [SerializeField, Min(0.1f)] private float interactionRange = 2.0f;

        [SerializeField] private LayerMask interactionMask = Physics.DefaultRaycastLayers;

        private InputAction interactAction;
        private Collider focusedCollider;

        /// <summary>The interactable currently under the crosshair, or null.</summary>
        public IInteractable CurrentTarget { get; private set; }

        /// <summary>
        /// Raised when the focused interactable changes (argument is null when nothing is focused)
        /// and after an interaction, because the target's prompt may have changed.
        /// </summary>
        public event Action<IInteractable> TargetChanged;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                Debug.LogError($"{nameof(PlayerInteractor)}: Camera Transform is not assigned.", this);
                enabled = false;
                return;
            }

            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
        }

        private void OnDisable() => SetFocus(null, null);

        private void Update()
        {
            // Temporary: no interaction while the cursor is released. Replaced by the pause flow later in Phase 1.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                SetFocus(null, null);
                return;
            }

            UpdateFocus();

            // WasPressedThisFrame ignores the "Hold" interaction configured on the template's Interact action,
            // so a normal key press is enough.
            if (CurrentTarget != null && interactAction.WasPressedThisFrame())
            {
                CurrentTarget.Interact(this);
                TargetChanged?.Invoke(CurrentTarget);
            }
        }

        private void UpdateFocus()
        {
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);

            // The ray starts inside the player's own CharacterController capsule; Physics.Raycast does not
            // report colliders that contain the ray origin, so the player never blocks its own ray.
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask, QueryTriggerInteraction.Ignore))
            {
                SetFocus(null, null);
                return;
            }

            // Same collider as last frame: skip the component lookup.
            if (hit.collider == focusedCollider)
            {
                return;
            }

            SetFocus(hit.collider, hit.collider.GetComponentInParent<IInteractable>());
        }

        private void SetFocus(Collider hitCollider, IInteractable target)
        {
            focusedCollider = hitCollider;

            if (ReferenceEquals(target, CurrentTarget))
            {
                return;
            }

            CurrentTarget = target;
            TargetChanged?.Invoke(target);
        }
    }
}
