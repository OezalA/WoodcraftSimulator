using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Economy
{
    /// <summary>
    /// Equipment upgrades bought with job income (Phase 8b, D-044): each is bought once and kept. Tools ask
    /// <see cref="Has"/> (e.g. the pull saw cuts faster); the lamp upgrade brightens the assigned lights here.
    /// Upgrades make work quicker or more comfortable, never required.
    /// </summary>
    public sealed class WorkshopUpgrades : MonoBehaviour
    {
        public const string PullSaw = "saw-pull";
        public const string BrushlessDrill = "drill-brushless";
        public const string LedLamp = "lamp-led";

        [Serializable]
        public sealed class Upgrade
        {
            public string id = "upgrade";
            public string displayName = "Upgrade";
            public string displayNameTurkish = string.Empty;
            public string effect = string.Empty;
            public string effectTurkish = string.Empty;
            [Min(0.01f)] public float priceEuros = 10f;

            [Tooltip("Rank needed to buy it (D-054).")]
            public Career.CareerRank requiredRank = Career.CareerRank.Novice;

            [Tooltip("Shown once owned (a workshop expansion, e.g. a second pair of sawhorses, D-054).")]
            public GameObject reveal;

            public string Name => Loc.Pick(displayName, displayNameTurkish);

            public string Effect => Loc.Pick(effect, effectTurkish);

            public Money Price => Money.FromEuros((decimal)Math.Round(priceEuros, 2));
        }

        [SerializeField] private WorkshopEconomy economy;
        [SerializeField] private Upgrade[] upgrades = Array.Empty<Upgrade>();

        [Header("Lamp upgrade")]
        [SerializeField] private Light[] lamps = Array.Empty<Light>();
        [SerializeField, Min(1f)] private float lampIntensityFactor = 1.8f;

        private readonly HashSet<string> owned = new HashSet<string>();
        private float[] baseIntensities;

        public IReadOnlyList<Upgrade> All => upgrades;

        public event Action Changed;

        private void Awake()
        {
            baseIntensities = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
            {
                baseIntensities[i] = lamps[i] != null ? lamps[i].intensity : 0f;
            }
        }

        public bool Has(string upgradeId) => upgradeId != null && owned.Contains(upgradeId);

        public PurchaseResult TryBuy(int index)
        {
            if (economy == null || index < 0 || index >= upgrades.Length || Has(upgrades[index].id))
            {
                return PurchaseResult.InvalidQuantity;
            }

            Upgrade upgrade = upgrades[index];
            if (!RankAllows(upgrade))
            {
                return PurchaseResult.InvalidQuantity;
            }

            if (!economy.Ledger.TrySpend(upgrade.Price, upgrade.Name))
            {
                return PurchaseResult.NotEnoughMoney;
            }

            owned.Add(upgrade.id);
            Apply();
            return PurchaseResult.Bought;
        }

        public string PromptFor(int index)
        {
            if (index < 0 || index >= upgrades.Length)
            {
                return name;
            }

            Upgrade upgrade = upgrades[index];
            if (!Has(upgrade.id) && !RankAllows(upgrade))
            {
                string rank = Career.CareerNames.Rank(upgrade.requiredRank);
                return Loc.T($"{upgrade.Name} ({upgrade.Effect}), {upgrade.Price}: needs rank {rank}",
                    $"{upgrade.Name} ({upgrade.Effect}), {upgrade.Price}: {rank} seviyesi gerekli");
            }

            return Has(upgrade.id)
                ? Loc.T($"Owned: {upgrade.Name} ({upgrade.Effect})", $"Sende var: {upgrade.Name} ({upgrade.Effect})")
                : Loc.T($"Upgrade: {upgrade.Name}, {upgrade.Effect}   {upgrade.Price}   (money {economy.Ledger.Balance})",
                    $"Yükseltme: {upgrade.Name}, {upgrade.Effect}   {upgrade.Price}   (para {economy.Ledger.Balance})");
        }

        public string[] Capture()
        {
            var ids = new List<string>(owned);
            ids.Sort(string.CompareOrdinal);
            return ids.ToArray();
        }

        public void Restore(IEnumerable<string> ids)
        {
            owned.Clear();
            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    owned.Add(id);
                }
            }

            Apply();
        }

        /// <summary>Whether the career's rank allows buying it (without a career: always).</summary>
        private static bool RankAllows(Upgrade upgrade) =>
            Career.WorkshopCareer.Active == null || Career.WorkshopCareer.Active.Progress.Rank >= upgrade.requiredRank;

        private void Apply()
        {
            foreach (Upgrade upgrade in upgrades)
            {
                if (upgrade.reveal != null)
                {
                    upgrade.reveal.SetActive(Has(upgrade.id));
                }
            }

            float factor = Has(LedLamp) ? lampIntensityFactor : 1f;
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] != null)
                {
                    lamps[i].intensity = baseIntensities[i] * factor;
                }
            }

            Changed?.Invoke();
        }
    }
}
