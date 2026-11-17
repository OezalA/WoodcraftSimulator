using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// HUD part for the economy (D-040, same UI document as <see cref="GameplayHud"/>): the money top right, and the
    /// workshop inventory (Player/Inventory, Tab) as a panel that stays open until toggled again. The inventory is
    /// refreshed a few times per second while open (it counts the boards lying around).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EconomyPanel : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        [SerializeField] private WorkshopEconomy economy;
        [SerializeField, Min(0.05f)] private float refreshSeconds = 0.5f;

        private Label moneyLabel;
        private Label inventoryLabel;
        private InputAction inventoryAction;
        private bool inventoryOpen;
        private float nextRefresh;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            moneyLabel = root.Q<Label>("money");
            inventoryLabel = root.Q<Label>("inventory");
            if (moneyLabel == null || inventoryLabel == null || economy == null)
            {
                Debug.LogError($"{nameof(EconomyPanel)}: needs 'money' and 'inventory' in the UXML and an Economy.", this);
                enabled = false;
                return;
            }

            inventoryAction = InputSystem.actions.FindAction("Player/Inventory", throwIfNotFound: true);
            economy.Changed += RefreshMoney;
            RefreshMoney();
        }

        private void OnDisable()
        {
            if (economy != null)
            {
                economy.Changed -= RefreshMoney;
            }
        }

        private void Update()
        {
            if (inventoryAction.WasPressedThisFrame())
            {
                inventoryOpen = !inventoryOpen;
                inventoryLabel.EnableInClassList(HiddenClass, !inventoryOpen);
                nextRefresh = 0f;
            }

            if (inventoryOpen && Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + refreshSeconds;
                string text = economy.InventoryText() + "\n" + Loc.T("[Tab] close", "[Tab] kapat");
                if (inventoryLabel.text != text)
                {
                    inventoryLabel.text = text;
                }
            }
        }

        private void RefreshMoney()
        {
            if (moneyLabel != null && economy.Ledger.IsStarted)
            {
                moneyLabel.text = Loc.T($"Money {economy.Ledger.Balance}   [Tab] inventory", $"Para {economy.Ledger.Balance}   [Tab] envanter");
                // In debt (D-054): the amount turns red.
                moneyLabel.EnableInClassList("money--debt", economy.Ledger.IsInDebt);
            }
        }
    }
}
