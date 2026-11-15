using UnityEngine;
using WoodcraftSimulator.Interaction;

namespace WoodcraftSimulator.Economy
{
    /// <summary>One equipment upgrade pinned on the order board (D-044): E buys it once.</summary>
    public sealed class UpgradeSheet : MonoBehaviour, IInteractable, IDescribable
    {
        [SerializeField] private WorkshopUpgrades upgrades;
        [SerializeField, Min(0)] private int upgradeIndex;

        private string lastResult;
        private float lastResultUntil;

        public string Description =>
            upgrades != null && upgradeIndex < upgrades.All.Count ? upgrades.All[upgradeIndex].Name : name;

        public string InteractionPrompt
        {
            get
            {
                if (upgrades == null)
                {
                    return name;
                }

                string prompt = upgrades.PromptFor(upgradeIndex);
                return lastResult != null && Time.unscaledTime < lastResultUntil ? prompt + "\n" + lastResult : prompt;
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (upgrades == null)
            {
                return;
            }

            PurchaseResult result = upgrades.TryBuy(upgradeIndex);
            lastResult = result == PurchaseResult.InvalidQuantity ? null : WorkshopEconomy.PurchaseText(result);
            lastResultUntil = Time.unscaledTime + 3f;
        }
    }
}
