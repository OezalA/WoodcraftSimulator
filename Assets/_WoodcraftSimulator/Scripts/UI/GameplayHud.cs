using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Inventory;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// In-game HUD (UI Toolkit): crosshair, a single prompt line and the tool belt bar.
    /// The prompt shows the focused interaction ("[E] Pick up ...") or, while carrying, the carry controls.
    /// Layout lives in UI/Hud.uxml, styling in UI/Hud.uss; this class only toggles state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameplayHud : MonoBehaviour
    {
        private const string CrosshairFocusedClass = "crosshair--focused";
        private const string HiddenClass = "hidden";
        private const string KeyboardMouseGroup = "Keyboard&Mouse";

        private const string BeltSlotClass = "belt-slot";
        private const string BeltSlotEmptyClass = "belt-slot--empty";
        private const string BeltSlotSelectedClass = "belt-slot--selected";

        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerCarrier carrier;
        [SerializeField] private PlayerToolBelt toolBelt;

        private VisualElement crosshair;
        private Label promptLabel;
        private VisualElement beltContainer;
        private Label[] beltSlots;
        private string[] beltKeyLabels;
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
            beltContainer = root.Q<VisualElement>("belt");

            if (crosshair == null || promptLabel == null || beltContainer == null)
            {
                Debug.LogError($"{nameof(GameplayHud)}: expected elements 'crosshair', 'interaction-prompt' and 'belt' in the UXML.", this);
                enabled = false;
                return;
            }

            if (interactor == null || carrier == null || toolBelt == null)
            {
                Debug.LogError($"{nameof(GameplayHud)}: Player Interactor, Carrier and Tool Belt must be assigned.", this);
                enabled = false;
                return;
            }

            interactor.TargetChanged += OnTargetChanged;
            carrier.CarryingChanged += OnCarryingChanged;
            carrier.PlacementStatusChanged += Refresh;
            toolBelt.Changed += RefreshBelt;
            Refresh();
            RefreshBelt();
        }

        // The belt's slot count is only known after PlayerToolBelt.Awake; Start runs after every Awake in the scene.
        private void Start()
        {
            if (!enabled || toolBelt.Belt == null)
            {
                return;
            }

            int slotCount = toolBelt.Belt.SlotCount;
            beltSlots = new Label[slotCount];
            beltKeyLabels = new string[slotCount];
            beltContainer.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                InputAction slotAction = InputSystem.actions.FindAction($"Player/ToolSlot{i + 1}");
                beltKeyLabels[i] = slotAction != null ? KeyLabelOf(slotAction) : (i + 1).ToString();

                var slot = new Label { pickingMode = PickingMode.Ignore };
                slot.AddToClassList(BeltSlotClass);
                beltContainer.Add(slot);
                beltSlots[i] = slot;
            }

            RefreshBelt();
        }

        private void RefreshBelt()
        {
            if (beltSlots == null || toolBelt.Belt == null)
            {
                return;
            }

            for (int i = 0; i < beltSlots.Length; i++)
            {
                var item = toolBelt.Belt.GetItem(i);
                Label slot = beltSlots[i];
                slot.text = item != null ? $"{beltKeyLabels[i]}   {item.DisplayName}" : $"{beltKeyLabels[i]}   —";
                slot.EnableInClassList(BeltSlotEmptyClass, item == null);
                slot.EnableInClassList(BeltSlotSelectedClass, i == toolBelt.Belt.SelectedSlot);
            }
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

            if (toolBelt != null)
            {
                toolBelt.Changed -= RefreshBelt;
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

        private static string KeyLabel(string actionPath) =>
            KeyLabelOf(InputSystem.actions.FindAction(actionPath, throwIfNotFound: true));

        private static string KeyLabelOf(InputAction action) =>
            action.GetBindingDisplayString(
                InputBinding.MaskByGroup(KeyboardMouseGroup),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
    }
}
