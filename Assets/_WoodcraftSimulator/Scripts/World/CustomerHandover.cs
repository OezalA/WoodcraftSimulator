using UnityEngine;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.World
{
    /// <summary>
    /// The customer at a location (Phase 12.5, D-056): the finished structure is handed over here, not at the job
    /// board in the workshop (it stands here). Greybox: the house door. E when the job is complete delivers it (the
    /// pay, the week closes as usual); before that it says what is left.
    /// </summary>
    public sealed class CustomerHandover : MonoBehaviour, IInteractable, IDescribable
    {
        [SerializeField] private string customerName = "Customer";
        [SerializeField] private string customerNameTurkish = "Müşteri";

        public string Description => Loc.Pick(customerName, customerNameTurkish);

        public string InteractionPrompt
        {
            get
            {
                JobBoard jobs = Core.Services.Get<JobBoard>();
                SceneFlow flow = Core.Services.Get<SceneFlow>();
                string text;
                if (jobs == null || flow == null || jobs.ActiveJobLocation != flow.CurrentLocationId)
                {
                    text = Loc.T($"{Description}: no job of yours here", $"{Description}: burada işin yok");
                }
                else if (jobs.IsComplete)
                {
                    text = Loc.T($"Hand the {jobs.ActiveJob.DisplayName} over to {Description}: get paid {jobs.Settlement.Earned}",
                        $"{jobs.ActiveJob.DisplayName} işini {Description} teslim et: {jobs.Settlement.Earned} ödenir");
                }
                else
                {
                    text = Loc.T($"{Description}: \"Come back when it is finished.\"", $"{Description}: \"Bitince gel.\"");
                }

                string message = jobs != null ? jobs.LastMessage : null;
                return message != null ? $"{text}\n{message}" : text;
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            JobBoard jobs = Core.Services.Get<JobBoard>();
            SceneFlow flow = Core.Services.Get<SceneFlow>();
            if (jobs != null && flow != null && jobs.ActiveJobLocation == flow.CurrentLocationId)
            {
                jobs.TryDeliverHere();
            }
        }
    }
}
