using UnityEngine;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// One pier spot of the terrace site (D-046): E digs it out, levels it and fills it with gravel, so that the pier
    /// block of <see cref="SlotId"/> can be set. Shows a stake while untouched and a gravel bed when prepared.
    /// </summary>
    public sealed class SiteSpot : MonoBehaviour, IInteractable, IDescribable
    {
        [SerializeField] private SitePreparation site;

        [Tooltip("The blueprint slot of the pier block that stands here.")]
        [SerializeField] private string slotId = "pier-1";

        [SerializeField] private GameObject stakeVisual;
        [SerializeField] private GameObject gravelVisual;

        public string SlotId => slotId;

        public bool Prepared { get; set; }

        public string Description => Loc.T("Pier spot", "Ayak yeri");

        public string InteractionPrompt
        {
            get
            {
                if (site == null)
                {
                    return Description;
                }

                if (!site.Marked)
                {
                    return Loc.T("Pier spot: set out the site first (corner stake)", "Ayak yeri: önce alanı işaretle (köşe kazığı)");
                }

                return Prepared
                    ? Loc.T("Pier spot ready: set a concrete pier block here", "Ayak yeri hazır: buraya beton ayak bloğu koy")
                    : Loc.T("Dig, level and fill with gravel", "Kaz, düzle ve çakıl dök");
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (site != null && !Prepared)
            {
                site.Prepare(this);
            }
        }

        public void ShowState(bool marked)
        {
            if (stakeVisual != null)
            {
                stakeVisual.SetActive(!Prepared);
            }

            if (gravelVisual != null)
            {
                gravelVisual.SetActive(Prepared);
            }
        }
    }
}
