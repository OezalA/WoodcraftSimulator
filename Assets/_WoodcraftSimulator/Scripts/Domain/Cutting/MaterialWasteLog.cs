using System;

namespace WoodcraftSimulator.Cutting
{
    /// <summary>
    /// Running total of material lost to cutting (sawdust) in the workshop. Offcuts are not waste here: they stay
    /// usable pieces until the player discards them (Phase 7, material economy).
    /// </summary>
    public sealed class MaterialWasteLog
    {
        public int CutCount { get; private set; }

        public double TotalVolumeCubicMeters { get; private set; }

        public double TotalMassKg { get; private set; }

        /// <summary>Raised after a cut's waste was recorded.</summary>
        public event Action Changed;

        public void Record(CutWaste waste)
        {
            if (waste.VolumeCubicMeters < 0.0 || waste.MassKg < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(waste), "Waste must not be negative.");
            }

            CutCount++;
            TotalVolumeCubicMeters += waste.VolumeCubicMeters;
            TotalMassKg += waste.MassKg;
            Changed?.Invoke();
        }

        /// <summary>Sets the totals (loading a save). Replaces whatever was recorded before.</summary>
        public void Restore(int cutCount, double totalVolumeCubicMeters, double totalMassKg)
        {
            if (cutCount < 0 || !(totalVolumeCubicMeters >= 0.0) || !(totalMassKg >= 0.0) ||
                double.IsInfinity(totalVolumeCubicMeters) || double.IsInfinity(totalMassKg))
            {
                throw new ArgumentOutOfRangeException(nameof(cutCount), "Waste totals must be finite and not negative.");
            }

            CutCount = cutCount;
            TotalVolumeCubicMeters = totalVolumeCubicMeters;
            TotalMassKg = totalMassKg;
            Changed?.Invoke();
        }
    }
}
