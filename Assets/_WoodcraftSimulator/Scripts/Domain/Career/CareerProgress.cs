using System;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Career
{
    /// <summary>The player's standing as a craftsman (Phase 12, D-054), from experience.</summary>
    public enum CareerRank
    {
        Novice,
        Journeyman,
        Master,
        MasterCraftsman
    }

    /// <summary>
    /// Areas of experience (D-054). They grow by doing (parts cut, fasteners driven, parts finished, outdoor parts set),
    /// open job types and make that kind of work a little quicker; never a test of the player's hand.
    /// </summary>
    public enum Skill
    {
        Cutting,
        Joining,
        Finishing,
        Outdoor
    }

    /// <summary>What a delivered job brings for the career.</summary>
    public sealed class JobOutcome
    {
        /// <param name="basePay">The job's pay before the reputation factor (experience follows the size of the job).</param>
        public JobOutcome(Money basePay, int goalsMet, int goals, int parts, int fasteners, bool finished, bool outdoor)
        {
            BasePay = basePay;
            GoalsMet = Math.Max(0, goalsMet);
            Goals = Math.Max(0, goals);
            Parts = Math.Max(0, parts);
            Fasteners = Math.Max(0, fasteners);
            Finished = finished;
            Outdoor = outdoor;
        }

        public Money BasePay { get; }

        public int GoalsMet { get; }

        public int Goals { get; }

        public int Parts { get; }

        public int Fasteners { get; }

        public bool Finished { get; }

        public bool Outdoor { get; }

        /// <summary>Share of the quality goals met (1 when the job had none).</summary>
        public double Quality => Goals == 0 ? 1.0 : Math.Min(1.0, (double)GoalsMet / Goals);
    }

    /// <summary>What changed when a job was delivered (for the message and the career card).</summary>
    public sealed class JobReview
    {
        internal JobReview(int experience, double reputationBefore, double reputationAfter, CareerRank rankBefore,
            CareerRank rankAfter, int[] skillLevelsBefore, int[] skillLevelsAfter)
        {
            Experience = experience;
            ReputationBefore = reputationBefore;
            ReputationAfter = reputationAfter;
            RankBefore = rankBefore;
            RankAfter = rankAfter;
            SkillLevelsBefore = skillLevelsBefore;
            SkillLevelsAfter = skillLevelsAfter;
        }

        public int Experience { get; }

        public double ReputationBefore { get; }

        public double ReputationAfter { get; }

        public CareerRank RankBefore { get; }

        public CareerRank RankAfter { get; }

        public bool RankedUp => RankAfter > RankBefore;

        private int[] SkillLevelsBefore { get; }

        private int[] SkillLevelsAfter { get; }

        public bool SkillUp(Skill skill) => SkillLevelsAfter[(int)skill] > SkillLevelsBefore[(int)skill];
    }

    /// <summary>
    /// Experience, reputation, skills and the week (Phase 12, D-054). One delivered job closes a week (owner's choice:
    /// no clock pressure). Experience follows the size of the job and its quality; reputation (0-5 stars) moves towards
    /// the quality of each job and sets the pay factor of new offers.
    /// </summary>
    public sealed class CareerProgress
    {
        /// <summary>Experience needed for each rank (index = rank).</summary>
        public static readonly int[] RankExperience = { 0, 100, 400, 1200 };

        /// <summary>Skill points needed for levels 1..5 (index = level - 1).</summary>
        public static readonly int[] SkillPoints = { 0, 15, 40, 90, 180 };

        public const double StartReputation = 2.0;
        public const double MaxReputation = 5.0;

        /// <summary>How far one job moves the reputation towards its own rating (0..1).</summary>
        public const double ReputationWeight = 0.35;

        /// <summary>Quicker work per skill level above 1 (level 5 = 20 % quicker).</summary>
        public const double SpeedPerLevel = 0.05;

        private readonly int[] skillPoints = new int[4];

        public CareerProgress()
        {
            Reputation = StartReputation;
            Week = 1;
        }

        public int Experience { get; private set; }

        public double Reputation { get; private set; }

        /// <summary>The current week, from 1 (each delivered job closes one).</summary>
        public int Week { get; private set; }

        public int JobsDelivered { get; private set; }

        public event Action Changed;

        public CareerRank Rank
        {
            get
            {
                var rank = CareerRank.Novice;
                for (int i = 0; i < RankExperience.Length; i++)
                {
                    if (Experience >= RankExperience[i])
                    {
                        rank = (CareerRank)i;
                    }
                }

                return rank;
            }
        }

        /// <summary>Experience of the next rank, or null at the top.</summary>
        public int? NextRankExperience =>
            (int)Rank + 1 < RankExperience.Length ? RankExperience[(int)Rank + 1] : (int?)null;

        public int Points(Skill skill) => skillPoints[(int)skill];

        public int Level(Skill skill) => LevelFor(skillPoints[(int)skill]);

        /// <summary>Points of the next level of the skill, or null at level 5.</summary>
        public int? NextLevelPoints(Skill skill)
        {
            int level = Level(skill);
            return level < SkillPoints.Length ? SkillPoints[level] : (int?)null;
        }

        /// <summary>How much quicker this kind of work goes (1.0 at level 1, 1.2 at level 5).</summary>
        public double SpeedFactor(Skill skill) => 1.0 + SpeedPerLevel * (Level(skill) - 1);

        /// <summary>Pay factor of new offers from the reputation: 0.85 at 0 stars, 1.0 at 2, 1.225 at 5.</summary>
        public double PayFactor => 0.85 + 0.075 * Reputation;

        public static int LevelFor(int points)
        {
            int level = 1;
            for (int i = 0; i < SkillPoints.Length; i++)
            {
                if (points >= SkillPoints[i])
                {
                    level = i + 1;
                }
            }

            return level;
        }

        /// <summary>A job was delivered: experience, reputation, skills; the week closes.</summary>
        public JobReview Complete(JobOutcome outcome)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));

            CareerRank rankBefore = Rank;
            double reputationBefore = Reputation;
            int[] levelsBefore = Levels();

            // Experience: one point per euro of the job's size, half of it for the quality goals.
            int experience = (int)Math.Round(outcome.BasePay.Cents / 100.0 * (0.5 + 0.5 * outcome.Quality));
            Experience += Math.Max(0, experience);

            double rating = MaxReputation * outcome.Quality;
            Reputation = Math.Max(0.0, Math.Min(MaxReputation, Reputation + (rating - Reputation) * ReputationWeight));

            skillPoints[(int)Skill.Cutting] += outcome.Parts;
            skillPoints[(int)Skill.Joining] += outcome.Fasteners;
            if (outcome.Finished)
            {
                skillPoints[(int)Skill.Finishing] += outcome.Parts;
            }

            if (outcome.Outdoor)
            {
                skillPoints[(int)Skill.Outdoor] += outcome.Parts;
            }

            JobsDelivered++;
            Week++;
            Changed?.Invoke();
            return new JobReview(Math.Max(0, experience), reputationBefore, Reputation, rankBefore, Rank, levelsBefore, Levels());
        }

        public void Restore(int experience, double reputation, int week, int jobsDelivered, int[] points)
        {
            Experience = Math.Max(0, experience);
            Reputation = double.IsNaN(reputation) ? StartReputation : Math.Max(0.0, Math.Min(MaxReputation, reputation));
            Week = Math.Max(1, week);
            JobsDelivered = Math.Max(0, jobsDelivered);
            for (int i = 0; i < skillPoints.Length; i++)
            {
                skillPoints[i] = points != null && i < points.Length ? Math.Max(0, points[i]) : 0;
            }

            Changed?.Invoke();
        }

        public int[] CapturePoints() => (int[])skillPoints.Clone();

        private int[] Levels()
        {
            var levels = new int[skillPoints.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = LevelFor(skillPoints[i]);
            }

            return levels;
        }
    }

    /// <summary>Names for the HUD (D-054).</summary>
    public static class CareerNames
    {
        public static string Rank(CareerRank rank) => rank switch
        {
            CareerRank.Journeyman => Loc.T("Journeyman", "Kalfa"),
            CareerRank.Master => Loc.T("Master", "Usta"),
            CareerRank.MasterCraftsman => Loc.T("Master craftsman", "Başusta"),
            _ => Loc.T("Novice", "Acemi")
        };

        public static string Skill(Skill skill) => skill switch
        {
            Career.Skill.Joining => Loc.T("Joining", "Birleştirme"),
            Career.Skill.Finishing => Loc.T("Finishing", "Yüzey işleri"),
            Career.Skill.Outdoor => Loc.T("Outdoor building", "Dış yapı"),
            _ => Loc.T("Cutting", "Kesim")
        };
    }

    /// <summary>
    /// The workshop's weekly running costs (D-054), charged when a week closes (a job is delivered): rent, and the
    /// apprentice's wage once he is hired. May take the balance below zero (debt, owner's choice: the game goes on).
    /// </summary>
    public static class RunningCosts
    {
        public static readonly Money Rent = Money.FromEuros(15m);
        public static readonly Money ApprenticeWage = Money.FromEuros(20m);

        public static Money Weekly(bool apprenticeHired) => apprenticeHired ? Rent + ApprenticeWage : Rent;
    }
}
