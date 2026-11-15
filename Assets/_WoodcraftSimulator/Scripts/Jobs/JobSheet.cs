using UnityEngine;
using WoodcraftSimulator.Interaction;

namespace WoodcraftSimulator.Jobs
{
    /// <summary>One job's sheet pinned on the <see cref="JobBoard"/>: E accepts the job, orders more stock or delivers it.</summary>
    public sealed class JobSheet : MonoBehaviour, IInteractable, IDescribable
    {
        [SerializeField] private JobBoard board;
        [SerializeField, Min(0)] private int jobIndex;

        public JobBoard Board => board;

        public int JobIndex => jobIndex;

        public string InteractionPrompt => board != null ? board.PromptFor(jobIndex) : name;

        public string Description => board != null && jobIndex < board.Jobs.Count ? board.Jobs[jobIndex].DisplayName : name;

        public void Interact(PlayerInteractor interactor)
        {
            if (board != null)
            {
                board.Interact(jobIndex);
            }
        }
    }
}
