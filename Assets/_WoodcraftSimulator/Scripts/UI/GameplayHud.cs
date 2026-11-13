using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Interaction;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// In-game HUD (UI Toolkit): crosshair and a single prompt line.
    /// The prompt shows the focused interaction ("[E] Pick up ...") or, while carrying, the carry controls.
    /// Layout lives in UI/Hud.uxml, styling in UI/Hud.uss; this class only toggles state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameplayHud : MonoBehaviour
    {
        private const string CrosshairFocusedClass = "crosshair--focused";
        private const string HiddenClass = "hidden";
        private const string KeyboardMouseGroup = "Keyboard&Mouse";

        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerCarrier carrier;

        private VisualElement crosshair;
        private Label promptLabel;
        private string interactKeyLabel;
        private string carryHint;

        private void Awake()
        {
            // Show the actually bound keys (e.g. "E", "LMB") so prompts stay correct if bindings change.
            interactKeyLabel = KeyLabel("Player/Interact");
            carryHint = $"[{KeyLabel("Player/Place")}]  Place      " +
                        $"[{KeyLabel("Player/Rotate")}]  Rotate      " +
                        $"[{KeyLabel("Player/Tilt")}]  Tilt";
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

            if (interactor == null || carrier == null)
            {
                Debug.LogError($"{nameof(GameplayHud)}: Player Interactor and Player Carrier must be assigned.", this);
                enabled = false;
                return;
            }

            interactor.TargetChanged += OnTargetChanged;
            carrier.CarryingChanged += OnCarryingChanged;
            carrier.PlacementStatusChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (interactor != null)
            {
                interactor.TargetChanged -= OnTargetChanged;
            }

            if (carrier != null)
            {
                carrier.CarryingChanged -= OnCarryingChanged;
                carrier.PlacementStatusChanged -= Refresh;
            }
        }

        private void OnTargetChanged(IInteractable target) => Refresh();

        private void OnCarryingChanged(bool isCarrying) => Refresh();

        private void Refresh()
        {
            if (carrier.IsCarrying)
            {
                crosshair.EnableInClassList(CrosshairFocusedClass, false);
                string status = PlacementStatusText();
                ShowPrompt(status == null ? carryHint : $"{carryHint}\n{status}");
                return;
            }

            IInteractable target = interactor.CurrentTarget;
            crosshair.EnableInClassList(CrosshairFocusedClass, target != null);

            if (target != null)
            {
                ShowPrompt($"[{interactKeyLabel}]  {target.InteractionPrompt}");
            }
            else
            {
                promptLabel.EnableInClassList(HiddenClass, true);
            }
        }

        /// <summary>Why the carried object can't be placed right now; null when placement is possible.</summary>
        private string PlacementStatusText()
        {
            switch (carrier.Status)
            {
                case PlacementStatus.NoSurface:
                    return "Aim at a free, level surface within reach";
                case PlacementStatus.Blocked:
                    Collider blocker = carrier.Blocker;
                    if (blocker == null)
                    {
                        return "Blocked";
                    }

                    // The carrier lives on the player, so a blocker inside the player hierarchy is the player itself.
                    return blocker.transform.IsChildOf(carrier.transform)
                        ? "Blocked: too close to you"
                        : $"Blocked by {blocker.name}";
                default:
                    return null;
            }
        }

        private void ShowPrompt(string text)
        {
            promptLabel.text = text;
            promptLabel.EnableInClassList(HiddenClass, false);
        }

        private static string KeyLabel(string actionPath)
        {
            InputAction action = InputSystem.actions.FindAction(actionPath, throwIfNotFound: true);
            return action.GetBindingDisplayString(
                InputBinding.MaskByGroup(KeyboardMouseGroup),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }
    }
}
