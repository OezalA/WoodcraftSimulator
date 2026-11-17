using System;
using System.Collections.Generic;
using WoodcraftSimulator.Career;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Jobs
{
    /// <summary>A wood a customer may ask for instead of the template's own, and how much more the job then pays.</summary>
    public sealed class SpeciesOption
    {
        public SpeciesOption(string speciesId, string speciesName, double payFactor)
        {
            SpeciesId = speciesId ?? throw new ArgumentNullException(nameof(speciesId));
            SpeciesName = string.IsNullOrWhiteSpace(speciesName) ? speciesId : speciesName;
            PayFactor = payFactor > 0.0 ? payFactor : 1.0;
        }

        public string SpeciesId { get; }

        public string SpeciesName { get; }

        public double PayFactor { get; }
    }

    /// <summary>What the market needs to know about a job template (one per job definition, D-054).</summary>
    public sealed class JobTemplateInfo
    {
        public JobTemplateInfo(string id, string displayName, Money basePay, CareerRank requiredRank,
            Skill? requiredSkill = null, int requiredSkillLevel = 1, IEnumerable<SpeciesOption> speciesOptions = null,
            bool finishOptional = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            BasePay = basePay;
            RequiredRank = requiredRank;
            RequiredSkill = requiredSkill;
            RequiredSkillLevel = Math.Max(1, requiredSkillLevel);
            SpeciesOptions = new List<SpeciesOption>(speciesOptions ?? Array.Empty<SpeciesOption>()).AsReadOnly();
            FinishOptional = finishOptional;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public Money BasePay { get; }

        public CareerRank RequiredRank { get; }

        public Skill? RequiredSkill { get; }

        public int RequiredSkillLevel { get; }

        /// <summary>Other woods a customer may want (the template's own wood is always possible).</summary>
        public IReadOnlyList<SpeciesOption> SpeciesOptions { get; }

        /// <summary>A customer may ask for a sanded and oiled finish (pays more).</summary>
        public bool FinishOptional { get; }

        /// <summary>Why the career can't take this job yet, or null.</summary>
        public string LockReason(CareerProgress career)
        {
            if (career.Rank < RequiredRank)
            {
                return Loc.T($"needs rank {CareerNames.Rank(RequiredRank)}", $"{CareerNames.Rank(RequiredRank)} seviyesi gerekli");
            }

            if (RequiredSkill.HasValue && career.Level(RequiredSkill.Value) < RequiredSkillLevel)
            {
                string skill = CareerNames.Skill(RequiredSkill.Value);
                return Loc.T($"needs {skill} level {RequiredSkillLevel}", $"{skill} {RequiredSkillLevel}. seviye gerekli");
            }

            return null;
        }
    }

    /// <summary>
    /// One offer on the job board (D-054): a template, a customer, the wood and finish asked for, the pay. A locked offer
    /// shows a job the career can't take yet (a goal to work towards).
    /// </summary>
    public sealed class JobOffer
    {
        public JobOffer(string templateId, string customer, string speciesId, bool requiresFinish, Money pay,
            string lockReason = null)
        {
            TemplateId = templateId ?? throw new ArgumentNullException(nameof(templateId));
            Customer = customer ?? string.Empty;
            SpeciesId = speciesId;
            RequiresFinish = requiresFinish;
            Pay = pay;
            LockReason = lockReason;
        }

        public string TemplateId { get; }

        public string Customer { get; }

        /// <summary>The wood asked for; null = the template's own.</summary>
        public string SpeciesId { get; }

        /// <summary>The customer asks for a sanded and oiled finish (on top of what the template requires).</summary>
        public bool RequiresFinish { get; }

        public Money Pay { get; }

        public string LockReason { get; }

        public bool IsLocked => LockReason != null;
    }

    /// <summary>
    /// The job market (Phase 12, D-054): each week a set of offers is drawn from the templates the career may take,
    /// with a customer, sometimes another wood or a finish asked for, the pay scaled by those and by the reputation.
    /// The last slot shows the next locked job, if any, as a goal. Pure: the random numbers come from the caller.
    /// </summary>
    public static class JobMarket
    {
        /// <summary>Extra pay for a sanded and oiled finish asked for by the customer.</summary>
        public const double FinishPayFactor = 1.25;

        private const double OtherWoodChance = 0.4;
        private const double FinishChance = 0.35;

        public static readonly IReadOnlyList<string> Customers = new[]
        {
            "Ayşe Yılmaz", "Thomas Becker", "Elif Demir", "Jonas Weber", "Zeynep Kaya", "Lena Schmidt",
            "Murat Çelik", "Anna Fischer", "Can Öztürk", "Paul Wagner", "Selin Aydın", "Marie Hoffmann"
        };

        public static IReadOnlyList<JobOffer> Generate(IReadOnlyList<JobTemplateInfo> templates, CareerProgress career,
            int slots, Func<double> random)
        {
            if (templates == null) throw new ArgumentNullException(nameof(templates));
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var open = new List<JobTemplateInfo>();
            JobTemplateInfo nextLocked = null;
            foreach (JobTemplateInfo template in templates)
            {
                if (template.LockReason(career) == null)
                {
                    open.Add(template);
                }
                else if (nextLocked == null || template.BasePay < nextLocked.BasePay)
                {
                    nextLocked = template;
                }
            }

            var offers = new List<JobOffer>();
            if (slots <= 0 || open.Count == 0)
            {
                if (nextLocked != null && slots > 0)
                {
                    offers.Add(Locked(nextLocked, career, random));
                }

                return offers.AsReadOnly();
            }

            int openSlots = nextLocked != null ? slots - 1 : slots;
            // Every open template once (in a shuffled order) before any repeats, so the variety stays.
            var order = new List<JobTemplateInfo>();
            while (order.Count < openSlots)
            {
                var round = new List<JobTemplateInfo>(open);
                while (round.Count > 0 && order.Count < openSlots)
                {
                    int pick = Index(random(), round.Count);
                    order.Add(round[pick]);
                    round.RemoveAt(pick);
                }
            }

            var usedCustomers = new HashSet<string>();
            foreach (JobTemplateInfo template in order)
            {
                offers.Add(Open(template, career, random, usedCustomers));
            }

            if (nextLocked != null)
            {
                offers.Add(Locked(nextLocked, career, random));
            }

            return offers.AsReadOnly();
        }

        /// <summary>The pay of an offer: base × wood × finish × reputation, rounded to whole euros.</summary>
        public static Money PayFor(JobTemplateInfo template, SpeciesOption wood, bool finishAsked, double reputationFactor)
        {
            double factor = (wood?.PayFactor ?? 1.0) * (finishAsked ? FinishPayFactor : 1.0) * reputationFactor;
            long euros = (long)Math.Round(template.BasePay.Cents / 100.0 * factor);
            return Money.FromCents(euros * 100);
        }

        private static JobOffer Open(JobTemplateInfo template, CareerProgress career, Func<double> random,
            HashSet<string> usedCustomers)
        {
            SpeciesOption wood = null;
            if (template.SpeciesOptions.Count > 0 && random() < OtherWoodChance)
            {
                wood = template.SpeciesOptions[Index(random(), template.SpeciesOptions.Count)];
            }

            bool finish = template.FinishOptional && random() < FinishChance;
            return new JobOffer(template.Id, Customer(random, usedCustomers), wood?.SpeciesId, finish,
                PayFor(template, wood, finish, career.PayFactor));
        }

        private static JobOffer Locked(JobTemplateInfo template, CareerProgress career, Func<double> random) =>
            new JobOffer(template.Id, Customer(random, null), null, false, PayFor(template, null, false, career.PayFactor),
                template.LockReason(career));

        private static string Customer(Func<double> random, HashSet<string> used)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                string name = Customers[Index(random(), Customers.Count)];
                if (used == null || used.Add(name))
                {
                    return name;
                }
            }

            return Customers[Index(random(), Customers.Count)];
        }

        private static int Index(double random, int count) =>
            Math.Min(count - 1, Math.Max(0, (int)(random * count)));
    }
}
