using UnityEngine;
using UnityEngine.UIElements;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Jobs;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// HUD part for jobs (UI Toolkit, same document as <see cref="GameplayHud"/>): top left the job sheet of the active
    /// job (cut list with progress), or the offer of the job sheet being looked at; on the right the result once the
    /// structure is complete. Refreshed a few times per second (the cut list counts the pieces lying around).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class JobPanel : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        [SerializeField] private JobBoard jobBoard;
        [SerializeField] private PlayerInteractor interactor;

        [Tooltip("Seconds between refreshes of the job sheet.")]
        [SerializeField, Min(0.05f)] private float refreshSeconds = 0.5f;

        private Label sheetLabel;
        private Label resultLabel;
        private float nextRefresh;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            sheetLabel = root.Q<Label>("job-sheet");
            resultLabel = root.Q<Label>("job-result");
            if (sheetLabel == null || resultLabel == null || jobBoard == null || interactor == null)
            {
                Debug.LogError($"{nameof(JobPanel)}: needs 'job-sheet' and 'job-result' in the UXML, a Job Board and an Interactor.", this);
                enabled = false;
                return;
            }

            jobBoard.Changed += Refresh;
            interactor.TargetChanged += OnTargetChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (jobBoard != null)
            {
                jobBoard.Changed -= Refresh;
            }

            if (interactor != null)
            {
                interactor.TargetChanged -= OnTargetChanged;
            }
        }

        private void OnTargetChanged(IInteractable target) => Refresh();

        private void Update()
        {
            if (Time.unscaledTime >= nextRefresh)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + refreshSeconds;

            string sheet = jobBoard.SheetText();
            if (sheet == null && interactor.CurrentTarget is JobSheet jobSheet && jobSheet.Board == jobBoard)
            {
                sheet = jobBoard.OfferText(jobSheet.JobIndex);
            }

            Show(sheetLabel, sheet);

            JobResult result = jobBoard.Result;
            Show(resultLabel, result != null ? JobText.Result(result, FastenerNames(result)) : null);
        }

        private string FastenerNames(JobResult result)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var connection in result.Job.Blueprint.Connections)
            {
                string fastenerName = jobBoard.FastenerName(connection.FastenerId);
                if (!names.Contains(fastenerName))
                {
                    names.Add(fastenerName);
                }
            }

            return string.Join(", ", names);
        }

        private static void Show(Label label, string text)
        {
            bool visible = !string.IsNullOrEmpty(text);
            if (visible && label.text != text)
            {
                label.text = text;
            }

            label.EnableInClassList(HiddenClass, !visible);
        }
    }
}
