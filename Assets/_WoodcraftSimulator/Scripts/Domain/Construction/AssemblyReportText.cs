using System.Globalization;
using System.Text;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Construction
{
    /// <summary>
    /// Plain-text form of an <see cref="AssemblyQualityReport"/> for the HUD: every component on its own line,
    /// no single score (master prompt §6.12). Avoid symbols outside Latin-1 / Turkish letters (D-028, D-041).
    /// </summary>
    public static class AssemblyReportText
    {
        public static string SafetyNote => Loc.T("Describes this build only; not a structural safety check.",
            "Yalnızca bu yapıyı anlatır; yapısal güvenlik kontrolü değildir.");

        /// <param name="sawdustCubicCentimeters">Material lost to cutting so far, or null if unknown.</param>
        public static string Format(Blueprint blueprint, AssemblyQualityReport report, string fastenerName,
            double? sawdustCubicCentimeters, bool includeSafetyNote = true)
        {
            var text = new StringBuilder();
            text.Append(blueprint.DisplayName).Append(": ")
                .Append(report.IsComplete ? Loc.T("COMPLETE", "TAMAMLANDI") : Loc.T("in progress", "devam ediyor")).Append('\n');
            text.Append(Loc.T(
                $"Parts {report.PartsInstalled}/{report.PartsRequired} installed, {report.AccurateParts}/{report.PartsInstalled} accurate ({blueprint.AccuracyTolerance})\n",
                $"Parçalar: {report.PartsInstalled}/{report.PartsRequired} takıldı, {report.AccurateParts}/{report.PartsInstalled} ölçüsünde ({blueprint.AccuracyTolerance})\n"));

            foreach (PartQuality part in report.Parts)
            {
                text.Append("  ").Append(part.Slot.DisplayName).Append(": ");
                if (!part.IsInstalled)
                {
                    text.Append(Loc.T("missing", "eksik")).Append('\n');
                    continue;
                }

                Length deviation = part.Fit.Value.Length.Deviation;
                text.Append(part.Piece.Dimensions.Length);
                if (deviation != Length.Zero)
                {
                    text.Append(deviation > Length.Zero ? "  (+" : "  (").Append(deviation).Append(')');
                }

                text.Append(part.IsAccurate ? Loc.T("  OK\n", "  tamam\n") : Loc.T("  inaccurate\n", "  ölçü dışı\n"));
            }

            text.Append(Loc.T("Fasteners", "Vidalar")).Append($" {report.FastenersInstalled}/{report.FastenersRequired}");
            if (!string.IsNullOrEmpty(fastenerName))
            {
                text.Append(" (").Append(fastenerName).Append(')');
            }

            text.Append('\n');
            if (report.FastenersInstalled > 0)
            {
                text.Append(Loc.T($"  heads: {report.FlushFasteners} flush", $"  başlar: {report.FlushFasteners} hizada"));
                if (report.ProudFasteners > 0)
                {
                    text.Append(Loc.T($", {report.ProudFasteners} standing out", $", {report.ProudFasteners} dışarıda"));
                }

                if (report.OverdrivenFasteners > 0)
                {
                    text.Append(Loc.T($", {report.OverdrivenFasteners} driven too deep", $", {report.OverdrivenFasteners} fazla gömülü"));
                }

                text.Append('\n');
                if (report.Splits > 0)
                {
                    text.Append(Loc.T(
                        $"  {report.Splits} split: screwed near the end of a board without a pilot hole\n",
                        $"  {report.Splits} çatlak: tahta ucuna yakın yere pilot deliksiz vida\n"));
                }
            }

            if (sawdustCubicCentimeters.HasValue)
            {
                text.Append(Loc.T("Sawdust so far: ", "Şimdiye kadar talaş: "))
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
