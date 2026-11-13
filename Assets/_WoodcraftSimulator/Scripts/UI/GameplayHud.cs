using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using WoodcraftSimulator.Interaction;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// In-game HUD (UI Toolkit): crosshair and interaction prompt.
    /// Layout lives in UI/Hud.uxml, styling in UI/Hud.uss; this class only toggles state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameplayHud : MonoBehaviour
    {
        private const string CrosshairFocusedClass = "crosshair--focused";
        private const string HiddenClass = "hidden";

        [SerializeField] private PlayerInteractor interactor;

        private VisualElement crosshair;
        private Label promptLabel;
        private string interactKeyLabel;

        private void Awake()
        {
            // Show the actually bound key (e.g. "E") so the prompt stays correct if bindings change.
            InputAction interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            interactKeyLabel = interactAction.GetBindingDisplayString(
                InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            crosshair = root.Q<VisualElement>("crosshair");
            promptLabel = root.Q<Label>("interaction-prompt");

            if (crosshair == null || promptLabel == null)
            {
                Debug.LogError($"{nameof(GameplayHud)}: expected elements 'crosshair' and 'interaction-prompt' in the UXML.", this);
                enabled = false;
                return;
            }

            if (interactor == null)
            {
                Debug.LogError($"{nameof(GameplayHud)}: Player Interactor is not assigned.", this);
                enabled = false;
                return;
            }

            interactor.TargetChanged += OnTargetChanged;
            OnTargetChanged(interactor.CurrentTarget);
        }

        private void OnDisable()
        {
            if (interactor != null)
            {
                interactor.TargetChanged -= OnTargetChanged;
            }
        }

        private void OnTargetChanged(IInteractable target)
        {
            bool hasTarget = target != null;

            crosshair.EnableInClassList(CrosshairFocusedClass, hasTarget);
            promptLabel.EnableInClassList(HiddenClass, !hasTarget);

            if (hasTarget)
            {
                promptLabel.text = $"[{interactKeyLabel}]  {target.InteractionPrompt}";
            }
        }
    }
}
