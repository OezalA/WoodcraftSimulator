using UnityEngine;
using UnityEngine.UIElements;
using WoodcraftSimulator.Career;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// Small career card under the money (Phase 12, D-054): rank and week, an experience bar to the next rank, the
    /// reputation as five bars, the skill levels. Built once; updated when the career changes. Styling: Hud.uss,
    /// classes <c>career*</c>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CareerPanel : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        [SerializeField] private WorkshopCareer career;

        private VisualElement card;
        private Label rankLabel;
        private Label weekLabel;
        private VisualElement experienceFill;
        private Label experienceLabel;
        private readonly VisualElement[] stars = new VisualElement[5];
        private Label reputationLabel;
        private Label skillsLabel;

        private void OnEnable()
        {
            card = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("career-card");
            if (card == null || career == null)
            {
                Debug.LogError($"{nameof(CareerPanel)}: needs 'career-card' in the UXML and a Career.", this);
                enabled = false;
                return;
            }

            Build();
            career.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (career != null)
            {
                career.Changed -= Refresh;
            }
        }

        private void Build()
        {
            card.Clear();
            VisualElement top = Add(card, new VisualElement(), "career__row");
            rankLabel = Add(top, new Label(), "career__rank");
            Add(top, new VisualElement(), "career__spacer");
            weekLabel = Add(top, new Label(), "career__week");

            VisualElement bar = Add(card, new VisualElement(), "career__bar");
            experienceFill = Add(bar, new VisualElement(), "career__bar-fill");
            experienceLabel = Add(card, new Label(), "career__small");

            VisualElement reputation = Add(card, new VisualElement(), "career__row");
            Add(reputation, new Label(Loc.T("Reputation", "İtibar")), "career__small");
            for (int i = 0; i < stars.Length; i++)
            {
                VisualElement star = Add(reputation, new VisualElement(), "career__star");
                stars[i] = Add(star, new VisualElement(), "career__star-fill");
            }

            reputationLabel = Add(reputation, new Label(), "career__small");
            skillsLabel = Add(card, new Label(), "career__skills");
        }

        private void Refresh()
        {
            if (rankLabel == null)
            {
                return;
            }

            CareerProgress progress = career.Progress;
            rankLabel.text = CareerNames.Rank(progress.Rank);
            weekLabel.text = Loc.T($"Week {progress.Week}", $"{progress.Week}. hafta");

            int from = CareerProgress.RankExperience[(int)progress.Rank];
            int? to = progress.NextRankExperience;
            float share = to.HasValue ? Mathf.Clamp01((float)(progress.Experience - from) / (to.Value - from)) : 1f;
            experienceFill.style.width = Length.Percent(share * 100f);
            experienceLabel.text = to.HasValue
                ? Loc.T($"{progress.Experience}/{to.Value} XP to {CareerNames.Rank(progress.Rank + 1)}",
                    $"{CareerNames.Rank(progress.Rank + 1)} için {progress.Experience}/{to.Value} XP")
                : Loc.T($"{progress.Experience} XP, top rank", $"{progress.Experience} XP, en üst seviye");

            // Five bars, each filled by its share of the reputation (e.g. 3.4 = three full, one 40 %).
            for (int i = 0; i < stars.Length; i++)
            {
                float fill = Mathf.Clamp01((float)progress.Reputation - i);
                stars[i].style.width = Length.Percent(fill * 100f);
            }

            reputationLabel.text = progress.Reputation.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            skillsLabel.text = $"{CareerNames.Skill(Skill.Cutting)} {progress.Level(Skill.Cutting)}  ·  " +
                               $"{CareerNames.Skill(Skill.Joining)} {progress.Level(Skill.Joining)}  ·  " +
                               $"{CareerNames.Skill(Skill.Finishing)} {progress.Level(Skill.Finishing)}  ·  " +
                               $"{CareerNames.Skill(Skill.Outdoor)} {progress.Level(Skill.Outdoor)}";
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
