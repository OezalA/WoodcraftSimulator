using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Core;

namespace WoodcraftSimulator.Interaction
{
    /// <summary>
    /// Casts a ray from the player's eyes each frame, tracks the focused <see cref="IInteractable"/>
    /// and forwards the Interact input to it. UI listens to <see cref="TargetChanged"/> instead of polling.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;

        // 2.5 m from the eyes (~1.75 m above the floor) leaves ~1.9 m of horizontal reach for objects lying on the
        // floor; 2.0 m left only ~1.2 m, which made floor items feel unreachable.
        [Tooltip("Maximum reach from the eyes, in meters.")]
        [SerializeField, Min(0.1f)] private float interactionRange = 2.5f;

        [Tooltip("Radius (m) of the fallback sphere cast used when the precise ray misses every interactable. 0 = off.")]
        [SerializeField, Min(0f)] private float aimAssistRadius = 0.08f;

        [SerializeField] private LayerMask interactionMask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private InputAction interactAction;
        private InputAction adjustAction;
        private bool isBlocked;

        /// <summary>
        /// Temporarily disables interactions without disabling the component (e.g. hands full while carrying).
        /// Independent of the pause state, which enables/disables the component itself.
        /// </summary>
        public bool IsBlocked
        {
            get => isBlocked;
            set
            {
                isBlocked = value;
                if (isBlocked)
                {
                    SetFocus(null);
                }
            }
        }

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
            adjustAction = InputSystem.actions.FindAction("Player/AdjustTool", throwIfNotFound: true);
        }

        private void OnDisable() => SetFocus(null);

        // Disabled by PauseController while paused; OnDisable then clears the focus so the HUD prompt hides.
        private void Update()
        {
            if (isBlocked)
            {
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

            // The wheel goes to a looked-at object that uses it (an offcut bin's list, D-052).
            if (CurrentTarget is IScrollable scrollable && adjustAction.WasPerformedThisFrame())
            {
                float value = adjustAction.ReadValue<float>();
                if (!Mathf.Approximately(value, 0f))
                {
                    scrollable.Scroll(value > 0f ? 1 : -1);
                    TargetChanged?.Invoke(CurrentTarget);
                }
            }
        }

        private void UpdateFocus()
        {
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            IInteractable target = null;

            // The casts start inside the player's CharacterController, which they can report when looking down,
            // so the player's own hierarchy is skipped explicitly (PhysicsQueries).

            // 1) Precise ray: what is exactly under the crosshair wins (matters for small neighbouring targets).
            if (PhysicsQueries.RaycastIgnoring(ray, interactionRange, interactionMask, transform, hitBuffer,
                    out RaycastHit hit))
            {
                target = hit.collider.GetComponentInParent<IInteractable>();
            }

            // 2) Aim assist: if the ray missed every interactable, sweep a small sphere along it. Only the sphere's
            // first contact counts, so nothing behind a wall or shelf edge can be selected.
            if (target == null && aimAssistRadius > 0f &&
                PhysicsQueries.SphereCastIgnoring(ray, aimAssistRadius, interactionRange, interactionMask, transform,
                    hitBuffer, out RaycastHit assistHit))
            {
                target = assistHit.collider.GetComponentInParent<IInteractable>();
            }

            // GetComponentInParent with an interface type does not allocate; at most two lookups per frame.
            SetFocus(target);
        }

        private void SetFocus(IInteractable target)
        {
            if (ReferenceEquals(target, CurrentTarget))
            {
                return;
            }

            CurrentTarget = target;
            WheelFocus.HeldByTarget = target is IScrollable;
            TargetChanged?.Invoke(target);
        }
    }
}
