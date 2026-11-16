using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// Site preparation for an outdoor site (terrace Phase 10, D-046; pergola D-049): first the outline is set out with stakes and a
    /// string line (<see cref="SiteMarker"/>), then each pier spot is dug, levelled and filled with gravel
    /// (<see cref="SiteSpot"/>). A pier block only goes onto a prepared spot (the station asks <see cref="CanPlace"/>).
    /// Also names the construction stage (configurable <see cref="Stage"/>s) for the job sheet.
    /// </summary>
    public sealed class SitePreparation : MonoBehaviour
    {
        [SerializeField] private SiteSpot[] spots = Array.Empty<SiteSpot>();

        [Tooltip("The string line around the outline, shown once the site is set out.")]
        [SerializeField] private LineRenderer stringLine;

        [Tooltip("Steel brackets (visual only), each shown while the part of the same index in Bracket Slots is set " +
                 "(D-048): U beam brackets on the pier blocks, angle brackets beside the joists.")]
        [SerializeField] private GameObject[] bracketVisuals = Array.Empty<GameObject>();

        [SerializeField] private string[] bracketSlots = Array.Empty<string>();

        /// <summary>A construction stage: the parts whose slot ids start with <see cref="slotPrefix"/>.</summary>
        [Serializable]
        public sealed class Stage
        {
            public string slotPrefix = "pier";
            public string name = "foundation";
            public string nameTurkish = "temel";
        }

        [Tooltip("Construction stages after site preparation, bottom up (D-049). Default: the terrace.")]
        [SerializeField] private Stage[] stages =
        {
            new Stage { slotPrefix = "pier", name = "foundation (pier blocks)", nameTurkish = "temel (beton ayaklar)" },
            new Stage { slotPrefix = "beam", name = "beams", nameTurkish = "kirişler" },
            new Stage { slotPrefix = "joist", name = "joists", nameTurkish = "döşeme altı kirişleri" },
            new Stage { slotPrefix = "deck", name = "decking", nameTurkish = "döşeme tahtaları" }
        };

        public bool Marked { get; private set; }

        public event Action Changed;

        private AssemblyStation station;

        private void Awake() => TryGetComponent(out station);

        private void OnEnable()
        {
            if (station != null)
            {
                station.Changed += ShowBrackets;
            }
        }

        private void OnDisable()
        {
            if (station != null)
            {
                station.Changed -= ShowBrackets;
            }
        }

        private void Start() => Refresh();

        /// <summary>
        /// Each steel bracket shows while its part is set (D-048): the U bracket with its pier block (the beam then
        /// sits in it), the angle bracket with its joist (then screwed through its upright leg).
        /// </summary>
        private void ShowBrackets()
        {
            AssemblyBuild build = station != null ? station.Build : null;
            for (int i = 0; i < bracketVisuals.Length && i < bracketSlots.Length; i++)
            {
                if (bracketVisuals[i] != null)
                {
                    bracketVisuals[i].SetActive(build != null && build.GetPiece(bracketSlots[i]) != null);
                }
            }
        }

        public void Mark()
        {
            Marked = true;
            Refresh();
        }

        public void Prepare(SiteSpot spot)
        {
            if (Marked && spot != null)
            {
                spot.Prepared = true;
                Refresh();
            }
        }

        /// <summary>Whether a part may go into the slot: a pier needs its prepared spot (other slots are not gated).</summary>
        public bool CanPlace(string slotId, out string reason)
        {
            reason = null;
            foreach (SiteSpot spot in spots)
            {
                if (spot == null || spot.SlotId != slotId || spot.Prepared)
                {
                    continue;
                }

                reason = Marked
                    ? Loc.T("prepare this spot first: dig, level and fill with gravel (E on the spot)",
                        "önce bu yeri hazırla: kaz, düzle, çakıl dök (yerde E)")
                    : Loc.T("set out the site first: stakes and string line (E on the corner stake)",
                        "önce alanı işaretle: kazık ve ip (köşe kazığında E)");
                return false;
            }

            return true;
        }

        /// <summary>The construction stage, e.g. "Stage 3/5: beams (1/2)", from the parts installed.</summary>
        public string StageText(AssemblyBuild build)
        {
            int prepared = 0;
            foreach (SiteSpot spot in spots)
            {
                if (spot != null && spot.Prepared)
                {
                    prepared++;
                }
            }

            // Site preparation, then one stage per part group; fastening the rest counts with the last one.
            int count = stages.Length + 1;
            if (!Marked || prepared < spots.Length)
            {
                return Loc.T($"Stage 1/{count}: site preparation ({prepared}/{spots.Length} spots{(Marked ? string.Empty : ", set out first")})",
                    $"Aşama 1/{count}: zemin hazırlığı ({prepared}/{spots.Length} yer{(Marked ? string.Empty : ", önce işaretle")})");
            }

            for (int s = 0; s < stages.Length; s++)
            {
                int total = 0;
                int installed = 0;
                foreach (PartSlot slot in build.Blueprint.Slots)
                {
                    if (slot.Id.StartsWith(stages[s].slotPrefix, StringComparison.Ordinal))
                    {
                        total++;
                        if (build.GetPiece(slot.Id) != null)
                        {
                            installed++;
                        }
                    }
                }

                if (installed < total)
                {
                    return Loc.T($"Stage {s + 2}/{count}: {stages[s].name} ({installed}/{total})",
                        $"Aşama {s + 2}/{count}: {stages[s].nameTurkish} ({installed}/{total})");
                }
            }

            return build.IsComplete
                ? Loc.T("All stages done", "Tüm aşamalar bitti")
                : Loc.T($"Stage {count}/{count}: fasten everything", $"Aşama {count}/{count}: her şeyi sabitle");
        }

        /// <summary>Save state: "marked" and the slot ids of the prepared spots.</summary>
        public string[] Capture()
        {
            var done = new List<string>();
            if (Marked)
            {
                done.Add("marked");
            }

            foreach (SiteSpot spot in spots)
            {
                if (spot != null && spot.Prepared)
                {
                    done.Add(spot.SlotId);
                }
            }

            return done.ToArray();
        }

        public void Restore(IEnumerable<string> done)
        {
            var set = new HashSet<string>(done ?? Array.Empty<string>());
            Marked = set.Contains("marked");
            foreach (SiteSpot spot in spots)
            {
                if (spot != null)
                {
                    spot.Prepared = set.Contains(spot.SlotId);
                }
            }

            Refresh();
        }

        private void Refresh()
        {
            if (stringLine != null)
            {
                stringLine.enabled = Marked;
            }

            foreach (SiteSpot spot in spots)
            {
                if (spot != null)
                {
                    spot.ShowState(Marked);
                }
            }

            ShowBrackets();

            Changed?.Invoke();
        }
    }
}
