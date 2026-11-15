using System;

namespace WoodcraftSimulator.Cutting
{
    /// <summary>Tuning of <see cref="SawingProcess"/> (gameplay values, D-038).</summary>
    public sealed class SawingSettings
    {
        public SawingSettings(double strokeMillimetersPerSquareCentimeter = 120.0, double bladeLengthMillimeters = 500.0,
            double startFraction = 0.1, double startEfficiency = 0.5, double fastStrokeMillimetersPerSecond = 900.0,
            double fastStrokeDriftFactor = 4.0)
        {
            if (strokeMillimetersPerSquareCentimeter <= 0.0 || bladeLengthMillimeters <= 0.0 ||
                startFraction < 0.0 || startFraction >= 1.0 || startEfficiency <= 0.0 || startEfficiency > 1.0 ||
                fastStrokeMillimetersPerSecond <= 0.0 || fastStrokeDriftFactor < 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(strokeMillimetersPerSquareCentimeter), "Invalid sawing settings.");
            }

            StrokeMillimetersPerSquareCentimeter = strokeMillimetersPerSquareCentimeter;
            BladeLengthMillimeters = bladeLengthMillimeters;
            StartFraction = startFraction;
            StartEfficiency = startEfficiency;
            FastStrokeMillimetersPerSecond = fastStrokeMillimetersPerSecond;
            FastStrokeDriftFactor = fastStrokeDriftFactor;
        }

        /// <summary>Saw travel (mm) needed per cm² of cross-section in wood of 450 kg/m³ (denser wood needs more).</summary>
        public double StrokeMillimetersPerSquareCentimeter { get; }

        /// <summary>Usable stroke: movement beyond the blade's length does not cut.</summary>
        public double BladeLengthMillimeters { get; }

        /// <summary>Share of the cut that is the "start": the kerf isn't guiding the blade yet.</summary>
        public double StartFraction { get; }

        /// <summary>Starting is done with light strokes: cutting progress is reduced by this factor.</summary>
        public double StartEfficiency { get; }

        /// <summary>Strokes faster than this (mm/s) make the blade jump while starting.</summary>
        public double FastStrokeMillimetersPerSecond { get; }

        /// <summary>How much more sideways drift a fast stroke causes while starting.</summary>
        public double FastStrokeDriftFactor { get; }
    }

    /// <summary>
    /// A hand-saw cut driven by the player's strokes (D-038). Forward/back saw movement within the blade's length cuts;
    /// the work needed grows with the cross-section and the wood's density. While starting (before the kerf guides the
    /// blade), sideways movement of the hand moves the blade along the board, and fast strokes make it jump (more
    /// drift): the cut ends up where the blade really was. Afterwards the kerf holds the blade on its line.
    /// No randomness: the result depends only on the input.
    /// </summary>
    public sealed class SawingProcess
    {
        private readonly SawingSettings settings;
        private readonly double requiredStrokeMillimeters;

        public SawingProcess(double crossSectionSquareCentimeters, double densityKgPerCubicMeter, SawingSettings settings = null)
        {
            if (crossSectionSquareCentimeters <= 0.0 || densityKgPerCubicMeter <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(crossSectionSquareCentimeters), "Cross-section and density must be positive.");
            }

            this.settings = settings ?? new SawingSettings();
            requiredStrokeMillimeters = crossSectionSquareCentimeters * this.settings.StrokeMillimetersPerSquareCentimeter *
                                        (densityKgPerCubicMeter / 450.0);
        }

        /// <summary>0..1; the cut is through at 1.</summary>
        public double Progress { get; private set; }

        public bool IsDone => Progress >= 1.0;

        /// <summary>True until the kerf is deep enough to guide the blade.</summary>
        public bool IsStarting => Progress < settings.StartFraction;

        /// <summary>Saw position within the stroke, mm from the middle of the blade (for the animation).</summary>
        public double StrokePosition { get; private set; }

        /// <summary>How far the blade has wandered along the board since the start, mm (signed).</summary>
        public double DriftMillimeters { get; private set; }

        /// <summary>True if the last input was a fast stroke while starting (the blade jumped).</summary>
        public bool Jumped { get; private set; }

        /// <summary>Total saw travel needed for this cut (mm).</summary>
        public double RequiredStrokeMillimeters => requiredStrokeMillimeters;

        /// <param name="strokeMillimeters">Saw movement along the blade this frame (forward positive).</param>
        /// <param name="sidewaysMillimeters">Hand movement along the board this frame (wanders the blade while starting).</param>
        /// <param name="seconds">Duration of the frame (for the stroke speed).</param>
        public void Feed(double strokeMillimeters, double sidewaysMillimeters, double seconds)
        {
            if (seconds <= 0.0 || IsDone)
            {
                Jumped = false;
                return;
            }

            // Only the part of the movement that stays within the blade's length cuts.
            double half = settings.BladeLengthMillimeters / 2.0;
            double target = Math.Max(-half, Math.Min(half, StrokePosition + strokeMillimeters));
            double cutting = Math.Abs(target - StrokePosition);
            StrokePosition = target;

            bool starting = IsStarting;
            bool fast = Math.Abs(strokeMillimeters) / seconds > settings.FastStrokeMillimetersPerSecond;
            Jumped = starting && fast && cutting > 0.0;

            if (starting)
            {
                DriftMillimeters += sidewaysMillimeters * (fast ? settings.FastStrokeDriftFactor : 1.0);
                cutting *= settings.StartEfficiency;
            }

            Progress = Math.Min(1.0, Progress + cutting / requiredStrokeMillimeters);
        }
    }
}
