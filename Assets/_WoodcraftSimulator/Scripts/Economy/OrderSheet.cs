using UnityEngine;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Economy
{
    /// <summary>
    /// One item of the merchant's catalog pinned on the order board (D-040): E buys one unit. Boards are delivered to
    /// the delivery area at once, a fastener box goes into the workshop's stock.
    /// </summary>
    public sealed class OrderSheet : MonoBehaviour, IInteractable, IDescribable
    {
        [SerializeField] private WorkshopEconomy economy;
        [SerializeField, Min(0)] private int itemIndex;

        private string lastResult;
        private float lastResultUntil;

        private CatalogItem Item =>
            economy != null && economy.Catalog != null && itemIndex < economy.Catalog.Items.Count
                ? economy.Catalog.Items[itemIndex]
                : null;

        public string Description => Item != null ? Item.DisplayName : name;

        public string InteractionPrompt
        {
            get
            {
                CatalogItem item = Item;
                if (item == null)
                {
                    return name;
                }

                string prompt = Loc.T($"Buy: {item.DisplayName}   {item.UnitPrice}   (money {economy.Ledger.Balance})",
                    $"Satın al: {item.DisplayName}   {item.UnitPrice}   (para {economy.Ledger.Balance})");
                if (lastResult != null && Time.unscaledTime < lastResultUntil)
                {
                    prompt += "\n" + lastResult;
                }

                return prompt;
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            CatalogItem item = Item;
            if (item == null)
            {
                return;
            }

            PurchaseResult result = economy.Buy(item, 1);
            lastResult = result == PurchaseResult.Bought
                ? Loc.T("Bought: ", "Satın alındı: ") + economy.DeliveryText(item)
                : WorkshopEconomy.PurchaseText(result);
            lastResultUntil = Time.unscaledTime + 3f;
        }
    }
}
