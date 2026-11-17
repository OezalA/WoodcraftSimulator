using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// The corner stake of an outdoor site. First E sets out the outline with stakes and a string line (D-046); after
    /// that E asks the customer to inspect the stage just finished (D-053): approved, or the problems to fix first.
    /// What he said shows under the prompt for a few seconds (and on the job card).
    /// </summary>
    public sealed class SiteMarker : MonoBehaviour, IInteractable, IDescribable
    {
        [SerializeField] private SitePreparation site;

        [Tooltip("What is set out, e.g. \"the terrace (2.4 × 1.8 m)\".")]
        [SerializeField] private string outline = "the terrace (2.4 × 1.8 m)";

        [SerializeField] private string outlineTurkish = "terası (2,4 × 1,8 m)";

        [SerializeField, Min(1f)] private float messageSeconds = 6f;

        private string message;
        private float messageUntil;

        public string Description => Loc.T("Corner stake", "Köşe kazığı");

        public string InteractionPrompt
        {
            get
            {
                string text = Prompt();
                return message != null && Time.unscaledTime < messageUntil ? $"{text}\n{message}" : text;
            }
        }

        private string Prompt()
        {
            if (site == null)
            {
                return Description;
            }

            if (!site.Marked)
            {
                return Loc.T($"Set out {outline} with stakes and a string line",
                    $"{Capital(outlineTurkish)} kazık ve iple işaretle");
            }

            AssemblyBuild build = site.TryGetComponent(out AssemblyStation station) ? station.Build : null;
            int pending = build != null ? site.PendingInspection(build) : -1;
            return pending >= 0
                ? Loc.T($"Ask the customer to inspect the {site.StageName(pending)}",
                    $"Müşteriden denetim iste: {site.StageName(pending)}")
                : Loc.T("The site is set out; every stage so far is approved", "Alan işaretli; aşamalar şimdiye kadar onaylı");
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (site == null)
            {
                return;
            }

            if (!site.Marked)
            {
                site.Mark();
                return;
            }

            message = site.RequestInspection();
            messageUntil = Time.unscaledTime + messageSeconds;
        }

        private static string Capital(string text) =>
            string.IsNullOrEmpty(text) ? text : (Loc.IsTurkish && text[0] == 'i' ? 'İ' : char.ToUpperInvariant(text[0])) + text.Substring(1);
    }
}
