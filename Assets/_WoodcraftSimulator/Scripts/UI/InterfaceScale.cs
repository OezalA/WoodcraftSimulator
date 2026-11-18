using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// The interface size setting (Phase 13e, D-062). All UI documents share one <see cref="PanelSettings"/> asset that
    /// scales the 1920×1080 design with the window; a larger interface is the same design laid out for a smaller
    /// reference resolution. The asset itself is not changed (in the Editor that would be written back to the project):
    /// the documents get a runtime copy, made once per asset.
    /// </summary>
    public static class InterfaceScale
    {
        private sealed class Scaled
        {
            public PanelSettings Copy;
            public Vector2Int Reference;
        }

        private static readonly Dictionary<PanelSettings, Scaled> Copies = new Dictionary<PanelSettings, Scaled>();

        /// <summary>Gives every UI document in the loaded scenes the size <paramref name="scale"/> (1 = designed size).</summary>
        public static void Apply(float scale)
        {
            scale = Mathf.Max(0.1f, scale);
            foreach (UIDocument document in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                PanelSettings settings = document.panelSettings;
                if (settings == null)
                {
                    continue;
                }

                // The designed size needs no copy while the document still uses the asset.
                if (Mathf.Approximately(scale, 1f) && !IsCopy(settings))
                {
                    continue;
                }

                Scaled scaled = CopyOf(settings);
                if (document.panelSettings != scaled.Copy)
                {
                    document.panelSettings = scaled.Copy; // the document's elements move to the copy's panel as they are
                }

                scaled.Copy.referenceResolution = new Vector2Int(
                    Mathf.Max(1, Mathf.RoundToInt(scaled.Reference.x / scale)),
                    Mathf.Max(1, Mathf.RoundToInt(scaled.Reference.y / scale)));
            }
        }

        private static bool IsCopy(PanelSettings settings) => Find(settings) != null;

        private static Scaled Find(PanelSettings copy)
        {
            foreach (Scaled each in Copies.Values)
            {
                if (each.Copy == copy)
                {
                    return each;
                }
            }

            return null;
        }

        private static Scaled CopyOf(PanelSettings settings)
        {
            Scaled own = Find(settings);
            if (own != null)
            {
                return own;
            }

            // A copy is not an asset: unloading unused assets (a full scene reload) may destroy it, then a new one is made.
            if (Copies.TryGetValue(settings, out Scaled existing) && existing.Copy != null)
            {
                return existing;
            }

            PanelSettings copy = Object.Instantiate(settings);
            copy.name = settings.name + " (scaled)";
            var scaled = new Scaled { Copy = copy, Reference = settings.referenceResolution };
            Copies[settings] = scaled;
            return scaled;
        }
    }
}
