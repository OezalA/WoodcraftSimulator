using System;
using UnityEngine;

namespace WoodcraftSimulator.Economy
{
    /// <summary>
    /// What owned upgrades look like in the workshop scene (D-044, D-054, D-056): the LED lamp brightens the bench
    /// light, a workshop expansion (a second pair of sawhorses) is shown once bought. The upgrades themselves are owned
    /// in the core scene (<see cref="WorkshopUpgrades"/>); this follows them.
    /// </summary>
    public sealed class UpgradeVisuals : MonoBehaviour
    {
        [Serializable]
        public sealed class Reveal
        {
            public string upgradeId;
            public GameObject shown;
        }

        [SerializeField] private Light[] lamps = Array.Empty<Light>();
        [SerializeField, Min(1f)] private float lampIntensityFactor = 1.8f;
        [SerializeField] private Reveal[] reveals = Array.Empty<Reveal>();

        private WorkshopUpgrades upgrades;
        private float[] baseIntensities;

        private void Awake()
        {
            baseIntensities = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
            {
                baseIntensities[i] = lamps[i] != null ? lamps[i].intensity : 0f;
            }
        }

        private void OnEnable()
        {
            upgrades = Core.Services.Get<WorkshopUpgrades>();
            if (upgrades != null)
            {
                upgrades.Changed += Apply;
            }

            Apply();
        }

        private void OnDisable()
        {
            if (upgrades != null)
            {
                upgrades.Changed -= Apply;
            }
        }

        private void Apply()
        {
            bool Has(string id) => upgrades != null && upgrades.Has(id);

            float factor = Has(WorkshopUpgrades.LedLamp) ? lampIntensityFactor : 1f;
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] != null)
                {
                    lamps[i].intensity = baseIntensities[i] * factor;
                }
            }

            foreach (Reveal reveal in reveals)
            {
                if (reveal?.shown != null)
                {
                    reveal.shown.SetActive(Has(reveal.upgradeId));
                }
            }
        }
    }
}
