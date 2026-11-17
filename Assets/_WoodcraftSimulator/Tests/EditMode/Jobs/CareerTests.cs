using System.Linq;
using NUnit.Framework;
using WoodcraftSimulator.Career;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Jobs;

namespace WoodcraftSimulator.Tests.Jobs
{
    /// <summary>Career progress and the job market (Phase 12, D-054).</summary>
    public sealed class CareerTests
    {
        private static Money Eur(decimal euros) => Money.FromEuros(euros);

        private static JobOutcome Outcome(decimal pay, int met, int goals, int parts = 4, int fasteners = 12,
            bool finished = false, bool outdoor = false) =>
            new JobOutcome(Eur(pay), met, goals, parts, fasteners, finished, outdoor);

        [Test]
        public void NewCareer_NoviceWeekOneTwoStars()
        {
            var career = new CareerProgress();

            Assert.That(career.Rank, Is.EqualTo(CareerRank.Novice));
            Assert.That(career.Week, Is.EqualTo(1));
            Assert.That(career.Reputation, Is.EqualTo(2.0));
            Assert.That(career.PayFactor, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(career.NextRankExperience, Is.EqualTo(100));
        }

        [Test]
        public void Complete_ExperienceFollowsSizeAndQuality_WeekCloses()
        {
            var career = new CareerProgress();

            JobReview perfect = career.Complete(Outcome(60m, 4, 4));
            Assert.That(perfect.Experience, Is.EqualTo(60));
            JobReview poor = career.Complete(Outcome(60m, 0, 4));
            Assert.That(poor.Experience, Is.EqualTo(30), "Half for the job, half for the quality goals.");

            Assert.That(career.Experience, Is.EqualTo(90));
            Assert.That(career.Week, Is.EqualTo(3));
            Assert.That(career.JobsDelivered, Is.EqualTo(2));
        }

        [Test]
        public void Reputation_MovesTowardsEachJobsRating()
        {
            var career = new CareerProgress();

            JobReview good = career.Complete(Outcome(35m, 4, 4));
            Assert.That(good.ReputationAfter, Is.EqualTo(2.0 + (5.0 - 2.0) * 0.35).Within(1e-9));
            career.Complete(Outcome(35m, 0, 4));
            Assert.That(career.Reputation, Is.LessThan(good.ReputationAfter), "A poor job costs reputation.");
            Assert.That(career.Reputation, Is.GreaterThanOrEqualTo(0.0));
        }

        [Test]
        public void Rank_RisesWithExperience()
        {
            var career = new CareerProgress();
            JobReview review = career.Complete(Outcome(120m, 4, 4));

            Assert.That(review.RankedUp, Is.True);
            Assert.That(career.Rank, Is.EqualTo(CareerRank.Journeyman));
            career.Complete(Outcome(850m, 5, 5));
            Assert.That(career.Rank, Is.EqualTo(CareerRank.Master));
        }

        [Test]
        public void Skills_GrowByDoing_LevelGivesSpeed()
        {
            var career = new CareerProgress();
            JobReview review = career.Complete(Outcome(60m, 4, 4, parts: 16, fasteners: 40, finished: true, outdoor: false));

            Assert.That(career.Level(Skill.Cutting), Is.EqualTo(2), "16 parts.");
            Assert.That(career.Level(Skill.Joining), Is.EqualTo(3), "40 fasteners.");
            Assert.That(career.Level(Skill.Finishing), Is.EqualTo(2));
            Assert.That(career.Level(Skill.Outdoor), Is.EqualTo(1));
            Assert.That(review.SkillUp(Skill.Joining), Is.True);
            Assert.That(career.SpeedFactor(Skill.Joining), Is.EqualTo(1.10).Within(1e-9));
            Assert.That(CareerProgress.LevelFor(1000), Is.EqualTo(5));
        }

        [Test]
        public void Restore_KeepsEverything()
        {
            var career = new CareerProgress();
            career.Complete(Outcome(60m, 3, 4, parts: 5, fasteners: 20));
            var restored = new CareerProgress();
            restored.Restore(career.Experience, career.Reputation, career.Week, career.JobsDelivered, career.CapturePoints());

            Assert.That(restored.Experience, Is.EqualTo(career.Experience));
            Assert.That(restored.Reputation, Is.EqualTo(career.Reputation));
            Assert.That(restored.Week, Is.EqualTo(2));
            Assert.That(restored.Points(Skill.Joining), Is.EqualTo(20));
        }

        [Test]
        public void RunningCosts_RentPlusWageWhenHired()
        {
            Assert.That(RunningCosts.Weekly(false), Is.EqualTo(Eur(15m)));
            Assert.That(RunningCosts.Weekly(true), Is.EqualTo(Eur(35m)));
        }

        private static JobTemplateInfo[] Templates() => new[]
        {
            new JobTemplateInfo("frame", "Frame", Eur(35m), CareerRank.Novice),
            new JobTemplateInfo("shelf", "Shelf", Eur(60m), CareerRank.Novice,
                speciesOptions: new[] { new SpeciesOption("oak", "Oak", 1.5) }, finishOptional: true),
            new JobTemplateInfo("terrace", "Terrace", Eur(450m), CareerRank.Journeyman),
            new JobTemplateInfo("pergola", "Pergola", Eur(850m), CareerRank.Master, Skill.Outdoor, 2)
        };

        private static System.Func<double> Sequence(params double[] values)
        {
            int next = 0;
            return () => values[next++ % values.Length];
        }

        [Test]
        public void Market_OpenJobsFirst_NextLockedJobLast()
        {
            var career = new CareerProgress();
            var offers = JobMarket.Generate(Templates(), career, 5, Sequence(0.1, 0.7, 0.3, 0.9, 0.5));

            Assert.That(offers.Count, Is.EqualTo(5));
            Assert.That(offers.Take(4).All(o => !o.IsLocked && (o.TemplateId == "frame" || o.TemplateId == "shelf")), Is.True);
            Assert.That(offers[4].TemplateId, Is.EqualTo("terrace"), "The cheapest locked job is the goal shown.");
            StringAssert.Contains("Journeyman", offers[4].LockReason);
            Assert.That(offers.Count(o => o.TemplateId == "frame"), Is.EqualTo(2), "Both open jobs twice before a third.");
        }

        [Test]
        public void Market_SkillLock_AndPayFactors()
        {
            var career = new CareerProgress();
            career.Restore(500, 5.0, 4, 3, new[] { 0, 0, 0, 10 });

            Assert.That(career.Rank, Is.EqualTo(CareerRank.Master));
            StringAssert.Contains("Outdoor building level 2", Templates()[3].LockReason(career));

            JobTemplateInfo shelf = Templates()[1];
            Money pay = JobMarket.PayFor(shelf, shelf.SpeciesOptions[0], finishAsked: true, career.PayFactor);
            Assert.That(pay, Is.EqualTo(Eur(138m)), "60 × 1.5 oak × 1.25 finish × 1.225 reputation = 137.8 → 138.");
        }

        [Test]
        public void Market_CustomerMayAskForOtherWoodAndFinish()
        {
            var career = new CareerProgress();
            JobTemplateInfo[] shelfOnly = { Templates()[1] };
            // pick shelf, customer, wood yes (0.1 < 0.4), oak, finish yes (0.1 < 0.35)
            var offers = JobMarket.Generate(shelfOnly, career, 1, Sequence(0.0, 0.0, 0.1, 0.0, 0.1));

            Assert.That(offers[0].SpeciesId, Is.EqualTo("oak"));
            Assert.That(offers[0].RequiresFinish, Is.True);
            Assert.That(offers[0].Customer, Is.Not.Empty);
        }
    }
}
