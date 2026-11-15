using UnityEngine;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>The corner stake of the terrace site (D-046): E sets out the outline with stakes and a string line.</summary>
    public sealed class SiteMarker : MonoBehaviour, IInteractable, IDescribable
    {
        [SerializeField] private SitePreparation site;

        public string Description => Loc.T("Corner stake", "Köşe kazığı");

        public string InteractionPrompt => site != null && site.Marked
            ? Loc.T("The site is set out (string line)", "Alan işaretli (ip gerili)")
            : Loc.T("Set out the terrace (2.4 × 1.8 m) with stakes and a string line",
                "Terası (2,4 × 1,8 m) kazık ve iple işaretle");

        public void Interact(PlayerInteractor interactor)
        {
            if (site != null && !site.Marked)
            {
                site.Mark();
            }
        }
    }
}
