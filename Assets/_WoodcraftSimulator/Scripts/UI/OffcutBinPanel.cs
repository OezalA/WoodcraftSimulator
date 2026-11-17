using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Workshop;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// HUD card with the contents of an offcut bin (D-052): the bin looked at, otherwise the nearest one within
    /// <see cref="showDistance"/>. One row per size (count, size, surface); while the bin is looked at, the row chosen
    /// with the wheel is highlighted and taken with E. Same look as the job card (Hud.uss, <c>job-card*</c>, <c>bin-row*</c>).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class OffcutBinPanel : MonoBehaviour
    {
        private const string HiddenClass = "hidden";
        private const int MaxRows = 10;

        [SerializeField] private PlayerInteractor interactor;

        [Tooltip("The nearest bin within this horizontal distance (m) is shown even when not looked at.")]
        [SerializeField, Min(0.5f)] private float showDistance = 2.0f;

        [SerializeField, Min(0.05f)] private float refreshSeconds = 0.2f;

        private VisualElement card;
        private OffcutBin shown;
        private bool shownTargeted;
        private bool dirty;
        private float nextRefresh;

        private void OnEnable()
        {
            card = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("bin-card");
            if (card == null || interactor == null)
            {
                Debug.LogError($"{nameof(OffcutBinPanel)}: needs 'bin-card' in the UXML and an Interactor.", this);
                enabled = false;
                return;
            }

            interactor.TargetChanged += OnTargetChanged;
        }

        private void OnDisable()
        {
            if (interactor != null)
            {
                interactor.TargetChanged -= OnTargetChanged;
            }

            Show(null, false);
        }

        private void OnTargetChanged(IInteractable target) => nextRefresh = 0f;

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.unscaledTime + refreshSeconds;
            OffcutBin targeted = interactor.CurrentTarget as OffcutBin;
            Show(targeted != null ? targeted : Nearest(), targeted != null);
        }

        private OffcutBin Nearest()
        {
            OffcutBin nearest = null;
            float best = showDistance;
            Vector3 player = interactor.transform.position;
            foreach (OffcutBin bin in OffcutBin.All)
            {
                Vector3 offset = bin.transform.position - player;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance <= best)
                {
                    best = distance;
                    nearest = bin;
                }
            }

            return nearest;
        }

        private void Show(OffcutBin bin, bool targeted)
        {
            if (bin != shown)
            {
                if (shown != null)
                {
                    shown.Changed -= MarkDirty;
                }

                shown = bin;
                if (shown != null)
                {
                    shown.Changed += MarkDirty;
                }

                dirty = true;
            }

            if (targeted != shownTargeted)
            {
                shownTargeted = targeted;
                dirty = true;
            }

            if (card == null)
            {
                return;
            }

            card.EnableInClassList(HiddenClass, shown == null);
            if (shown != null && dirty)
            {
                Build();
            }

            dirty = false;
        }

        private void MarkDirty()
        {
            dirty = true;
            nextRefresh = 0f;
        }

        private void Build()
        {
            card.Clear();
            IReadOnlyList<OffcutGroup> groups = shown.Stock.Groups();

            VisualElement header = Add(card, new VisualElement(), "job-card__header");
            Add(header, new Label(Loc.T("OFFCUT BIN", "ARTIK KOVASI")), "job-card__eyebrow");
            Add(header, new Label(Loc.T($"{shown.Stock.Count} pieces", $"{shown.Stock.Count} parça")), "job-card__time");
            Add(card, new Label(shown.Species != null ? shown.Species.DisplayName : shown.name), "job-card__title");

            VisualElement section = Add(card, new VisualElement(), "job-card__section");
            if (groups.Count == 0)
            {
                Add(section, new Label(Loc.T("Empty. The apprentice puts offcuts in here.",
                    "Boş. Çırak artıkları buraya koyar.")), "job-card__description");
                return;
            }

            // Keep the chosen row visible when the list is longer than the card.
            int first = Mathf.Clamp(shown.Selected - MaxRows / 2, 0, Mathf.Max(0, groups.Count - MaxRows));
            int last = Mathf.Min(groups.Count, first + MaxRows);
            if (first > 0)
            {
                Add(section, new Label(Loc.T($"... {first} more above", $"... yukarıda {first} tane daha")), "job-card__note");
            }

            for (int i = first; i < last; i++)
            {
                OffcutGroup group = groups[i];
                VisualElement row = Add(section, new VisualElement(), "bin-row");
                row.EnableInClassList("bin-row--selected", shownTargeted && i == shown.Selected);
                Add(row, new Label($"{group.Count} ×"), "cut-row__qty");
                Add(row, new Label(OffcutBin.Describe(group)), "bin-row__size");
            }

            if (last < groups.Count)
            {
                Add(section, new Label(Loc.T($"... {groups.Count - last} more below", $"... aşağıda {groups.Count - last} tane daha")),
                    "job-card__note");
            }

            Add(card, new Label(shownTargeted
                ? Loc.T("[Wheel] choose   [E] take", "[Tekerlek] seç   [E] al")
                : Loc.T("Look at the bin to take a piece", "Parça almak için kovaya bak")), "bin-card__hint");
        }

        private static T Add<T>(VisualElement parent, T child, string className) where T : VisualElement
        {
            child.AddToClassList(className);
            child.pickingMode = PickingMode.Ignore;
            parent.Add(child);
            return child;
        }
    }
}
