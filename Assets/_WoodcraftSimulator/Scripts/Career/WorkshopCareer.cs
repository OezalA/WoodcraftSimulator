using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Persistence;

namespace WoodcraftSimulator.Career
{
    /// <summary>
    /// The player's career in the scene (Phase 12, D-054): the domain <see cref="CareerProgress"/> (experience,
    /// reputation, skills, week), the hired apprentice, and the weekly running costs charged when a delivered job
    /// closes a week. Tools ask <see cref="SpeedFactor"/> (skills make that kind of work a little quicker).
    /// </summary>
    public sealed class WorkshopCareer : MonoBehaviour
    {
        [SerializeField] private WorkshopEconomy economy;

        private static WorkshopCareer active;

        public CareerProgress Progress { get; } = new CareerProgress();

        public bool ApprenticeHired { get; private set; }

        /// <summary>The last week's review (shown on the career card until the next one), or null.</summary>
        public JobReview LastReview { get; private set; }

        /// <summary>Running costs charged when the last week closed.</summary>
        public Money LastCosts { get; private set; }

        /// <summary>Raised after the career, the week or the apprentice changed.</summary>
        public event Action Changed;

        /// <summary>The career of the running scene, or null.</summary>
        public static WorkshopCareer Active => active;

        /// <summary>How much quicker this kind of work goes for the career (1 without one).</summary>
        public static float SpeedFactor(Skill skill) => active != null ? (float)active.Progress.SpeedFactor(skill) : 1f;

        private void OnEnable()
        {
            active = this;
            Progress.Changed += RaiseChanged;
        }

        private void OnDisable()
        {
            Progress.Changed -= RaiseChanged;
            if (active == this)
            {
                active = null;
            }
        }

        /// <summary>A job was delivered: the career moves on, the week closes, rent (and wage) are charged.</summary>
        public JobReview CloseWeek(JobOutcome outcome)
        {
            int week = Progress.Week;
            LastReview = Progress.Complete(outcome);
            LastCosts = RunningCosts.Weekly(ApprenticeHired);
            if (economy != null && LastCosts > Money.Zero)
            {
                string what = ApprenticeHired ? Loc.T("rent and wage", "kira ve maaş") : Loc.T("rent", "kira");
                economy.Ledger.Charge(LastCosts, Loc.T($"Week {week}: {what}", $"{week}. hafta: {what}"));
            }

            RaiseChanged();
            return LastReview;
        }

        public void HireApprentice()
        {
            if (!ApprenticeHired)
            {
                ApprenticeHired = true;
                RaiseChanged();
            }
        }

        public CareerData Capture() => new CareerData
        {
            started = true,
            experience = Progress.Experience,
            reputation = Progress.Reputation,
            week = Progress.Week,
            jobsDelivered = Progress.JobsDelivered,
            skillPoints = Progress.CapturePoints(),
            apprenticeHired = ApprenticeHired
        };

        /// <summary>
        /// Loads the career. A save from before Phase 12 has none: it starts from the jobs already delivered (three
        /// quarters of their pay as experience, one week each).
        /// </summary>
        public void Restore(CareerData data, IReadOnlyList<Money> deliveredJobPays)
        {
            LastReview = null;
            LastCosts = Money.Zero;
            if (data != null && data.started)
            {
                Progress.Restore(data.experience, data.reputation, data.week, data.jobsDelivered, data.skillPoints);
                ApprenticeHired = data.apprenticeHired;
            }
            else
            {
                double experience = 0.0;
                foreach (Money pay in deliveredJobPays ?? Array.Empty<Money>())
                {
                    experience += pay.Cents / 100.0 * 0.75;
                }

                int jobs = deliveredJobPays?.Count ?? 0;
                Progress.Restore((int)Math.Round(experience), CareerProgress.StartReputation, jobs + 1, jobs, null);
                ApprenticeHired = false;
            }

            RaiseChanged();
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
