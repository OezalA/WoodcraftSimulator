using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// Site preparation for the outdoor terrace (Phase 10, D-046): first the outline is set out with stakes and a
    /// string line (<see cref="SiteMarker"/>), then each pier spot is dug, levelled and filled with gravel
    /// (<see cref="SiteSpot"/>). A pier block only goes onto a prepared spot (the station asks <see cref="CanPlace"/>).
    /// Also names the construction stage of the terrace for the job sheet.
    /// </summary>
    public sealed class SitePreparation : MonoBehaviour
    {
        [SerializeField] private SiteSpot[] spots = Array.Empty<SiteSpot>();

        [Tooltip("The string line around the outline, shown once the site is set out.")]
        [SerializeField] private LineRenderer stringLine;

        public bool Marked { get; private set; }

        public event Action Changed;

        private void Start() => Refresh();

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

            if (!Marked || prepared < spots.Length)
            {
                return Loc.T($"Stage 1/5: site preparation ({prepared}/{spots.Length} spots{(Marked ? string.Empty : ", set out first")})",
                    $"Aşama 1/5: zemin hazırlığı ({prepared}/{spots.Length} yer{(Marked ? string.Empty : ", önce işaretle")})");
            }

            (string prefix, string en, string tr)[] stages =
            {
                ("pier", "foundation (pier blocks)", "temel (beton ayaklar)"),
                ("beam", "beams", "kirişler"),
                ("joist", "joists", "döşeme altı kirişleri"),
                ("deck", "decking", "döşeme tahtaları")
            };

            for (int s = 0; s < stages.Length; s++)
            {
                int total = 0;
                int installed = 0;
                foreach (PartSlot slot in build.Blueprint.Slots)
                {
                    if (slot.Id.StartsWith(stages[s].prefix, StringComparison.Ordinal))
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
                    return Loc.T($"Stage {s + 2}/5: {stages[s].en} ({installed}/{total})",
                        $"Aşama {s + 2}/5: {stages[s].tr} ({installed}/{total})");
                }
            }

            return build.IsComplete
                ? Loc.T("All stages done", "Tüm aşamalar bitti")
                : Loc.T("Stage 5/5: fasten everything", "Aşama 5/5: her şeyi sabitle");
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

            Changed?.Invoke();
        }
    }
}
