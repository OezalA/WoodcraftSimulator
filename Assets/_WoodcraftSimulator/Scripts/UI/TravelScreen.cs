using UnityEngine;
using UnityEngine.UIElements;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.World;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// The travel screen (Phase 13e, D-062), part of the HUD document: covers everything while the van drives to a
    /// place and the place loads (also when the game starts), with the destination, a progress bar and a tip. It stays
    /// at least <see cref="minimumSeconds"/> so a fast load doesn't flash. Layout: UI/Hud.uxml "travel-screen".
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TravelScreen : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        [Tooltip("Shortest time on screen, in seconds.")]
        [SerializeField, Min(0f)] private float minimumSeconds = 1.2f;

        private static readonly string[][] Tips =
        {
            new[] { "Measure twice, cut once: a pencil mark saves a board.", "İki kere ölç, bir kere kes: kurşun kalem izi bir tahtayı kurtarır." },
            new[] { "A pilot hole near the end of a board keeps it from splitting.", "Tahta ucuna yakın vidada pilot delik çatlamayı önler." },
            new[] { "Every saw cut takes its kerf: the cut plan counts it.", "Her testere kesimi payını alır: kesim planı bunu hesaba katar." },
            new[] { "Offcuts are sorted into the bins by species: small parts can come from them.", "Artıklar türlerine göre kovalara ayrılır: küçük parçalar oradan çıkabilir." },
            new[] { "Quality goals pay a bonus on delivery.", "Kalite hedefleri teslimde ek ücret getirir." },
            new[] { "Material for a customer's job is brought to the customer's place.", "Müşteri işinin malzemesi müşterinin yerine getirilir." }
        };

        private VisualElement screen;
        private Label eyebrow;
        private Label title;
        private Label note;
        private VisualElement fill;
        private SceneFlow flow;
        private bool shown;
        private float shownAt;
        private int tip = -1;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            screen = root.Q<VisualElement>("travel-screen");
            eyebrow = root.Q<Label>("travel-eyebrow");
            title = root.Q<Label>("travel-title");
            note = root.Q<Label>("travel-note");
            fill = root.Q<VisualElement>("travel-fill");
            if (screen == null || eyebrow == null || title == null || note == null || fill == null)
            {
                Debug.LogError($"{nameof(TravelScreen)}: 'travel-screen' and its parts are missing from the UXML.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (flow == null)
            {
                flow = Core.Services.Get<SceneFlow>();
                if (flow == null)
                {
                    return;
                }
            }

            if (flow.IsTravelling && !shown)
            {
                Show();
            }

            if (!shown)
            {
                return;
            }

            fill.style.width = Length.Percent(Mathf.Clamp01(flow.Progress) * 100f);
            if (!flow.IsTravelling && Time.unscaledTime >= shownAt + minimumSeconds)
            {
                shown = false;
                screen.AddToClassList(HiddenClass);
            }
        }

        private void Show()
        {
            shown = true;
            shownAt = Time.unscaledTime;
            LocationDefinition destination = flow.Destination;
            eyebrow.text = flow.IsFirstLoad ? Loc.T("LOADING", "YÜKLENİYOR") : Loc.T("ON THE ROAD", "YOLDA");
            title.text = destination != null ? destination.DisplayName : string.Empty;
            tip = (tip + 1 + Random.Range(0, Tips.Length - 1)) % Tips.Length; // never the same tip twice in a row
            note.text = Loc.T("Tip: ", "İpucu: ") + Loc.Pick(Tips[tip][0], Tips[tip][1]);
            fill.style.width = Length.Percent(0f);
            screen.RemoveFromClassList(HiddenClass);
        }
    }
}
