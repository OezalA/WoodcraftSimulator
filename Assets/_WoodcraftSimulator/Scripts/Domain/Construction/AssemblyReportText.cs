using System.Globalization;
using System.Text;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Construction
{
    /// <summary>
    /// Plain-text form of an <see cref="AssemblyQualityReport"/> for the HUD: every component on its own line,
    /// no single score (master prompt §6.12). ASCII/Latin-1 only (the runtime font may lack other symbols).
    /// </summary>
    public static class AssemblyReportText
    {
        public const string SafetyNote = "Describes this build only; not a structural safety check.";

        /// <param name="sawdustCubicCentimeters">Material lost to cutting so far, or null if unknown.</param>
        public static string Format(Blueprint blueprint, AssemblyQualityReport report, string fastenerName,
            double? sawdustCubicCentimeters, bool includeSafetyNote = true)
        {
            var text = new StringBuilder();
            text.Append(blueprint.DisplayName).Append(": ").Append(report.IsComplete ? "COMPLETE" : "in progress").Append('\n');
            text.Append($"Parts {report.PartsInstalled}/{report.PartsRequired} installed, " +
                        $"{report.AccurateParts}/{report.PartsInstalled} accurate ({blueprint.AccuracyTolerance})\n");

            foreach (PartQuality part in report.Parts)
            {
                text.Append("  ").Append(part.Slot.DisplayName).Append(": ");
                if (!part.IsInstalled)
                {
                    text.Append("missing\n");
                    continue;
                }

                Length deviation = part.Fit.Value.Length.Deviation;
                text.Append(part.Piece.Dimensions.Length);
                if (deviation != Length.Zero)
                {
                    text.Append(deviation > Length.Zero ? "  (+" : "  (").Append(deviation).Append(')');
                }

                text.Append(part.IsAccurate ? "  OK\n" : "  inaccurate\n");
            }

            text.Append($"Fasteners {report.FastenersInstalled}/{report.FastenersRequired}");
            if (!string.IsNullOrEmpty(fastenerName))
            {
                text.Append(" (").Append(fastenerName).Append(')');
            }

            text.Append('\n');
            if (report.FastenersInstalled > 0)
            {
                text.Append($"  heads: {report.FlushFasteners} flush");
                if (report.ProudFasteners > 0)
                {
                    text.Append($", {report.ProudFasteners} standing out");
                }

                if (report.OverdrivenFasteners > 0)
                {
                    text.Append($", {report.OverdrivenFasteners} driven too deep");
                }

                text.Append('\n');
                if (report.Splits > 0)
                {
                    text.Append($"  {report.Splits} split: screwed near the end of a board without a pilot hole\n");
                }
            }

            if (sawdustCubicCentimeters.HasValue)
            {
                text.Append("Sawdust so far: ")
                    .Append(sawdustCubicCentimeters.Value.ToString("0.0", CultureInfo.InvariantCulture))
                    .Append(" cm³\n");
            }

            if (includeSafetyNote)
            {
                text.Append(SafetyNote);
            }

            return text.ToString().TrimEnd('\n');
        }
    }
}
