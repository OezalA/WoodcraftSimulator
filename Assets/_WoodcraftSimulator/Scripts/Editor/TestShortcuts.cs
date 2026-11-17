using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Career;
using WoodcraftSimulator.Jobs;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Editor-only shortcuts for the owner's manual tests (D-057), usable in Play Mode only; not part of the game.
    /// </summary>
    public static class TestShortcuts
    {
        [MenuItem("WoodcraftSimulator/Test (Play Mode)/Career: Master with Outdoor 2, money +500")]
        private static void MakeMaster()
        {
            var career = Object.FindFirstObjectByType<WorkshopCareer>();
            var jobs = Object.FindFirstObjectByType<JobBoard>();
            var economy = Object.FindFirstObjectByType<Economy.WorkshopEconomy>();
            if (career == null || jobs == null)
            {
                Debug.LogWarning("Start Play Mode first.");
                return;
            }

            CareerProgress progress = career.Progress;
            int[] points = progress.CapturePoints();
            points[(int)Skill.Outdoor] = Mathf.Max(points[(int)Skill.Outdoor], CareerProgress.SkillPoints[1]);
            progress.Restore(Mathf.Max(progress.Experience, CareerProgress.RankExperience[(int)CareerRank.Master]),
                progress.Reputation, progress.Week, progress.JobsDelivered, points);
            economy?.Ledger.Earn(Economy.Money.FromEuros(500m), "Test");
            jobs.RedrawOffers();
            Debug.Log("Test: career set to Master with Outdoor building 2, +€500, new offers.");
        }

        [MenuItem("WoodcraftSimulator/Test (Play Mode)/Career: Master with Outdoor 2, money +500", true)]
        private static bool CanMakeMaster() => Application.isPlaying;
    }
}
