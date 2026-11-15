using System;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Construction
{
    /// <summary>How a fastener is put in (D-045): each kind has its own tool.</summary>
    public enum FastenerKind
    {
        /// <summary>Driven with the drill-driver (clutch, head height).</summary>
        Screw,

        /// <summary>Hammered in, always flush. Splits near an end like a screw.</summary>
        Nail,

        /// <summary>Glued into a drilled hole; the joint holds once the glue has dried. Never splits.</summary>
        Dowel,

        /// <summary>A steel connector (joist hanger, post base) screwed on with the drill-driver; never splits (D-046).</summary>
        Connector
    }

    /// <summary>Wood glue (D-045): a doweled joint is clamped and must dry before it counts as finished.</summary>
    public static class GlueRules
    {
        /// <summary>Playing seconds a glued joint dries (shortened from real hours for play).</summary>
        public const double DryingSeconds = 45.0;
    }

    /// <summary>How a driven screw head ends up relative to the surface.</summary>
    public enum ScrewSeat
    {
        /// <summary>The head stands out of the surface (released too early, or the clutch slipped too soon).</summary>
        Proud,

        /// <summary>The head is level with the surface (within the tolerance).</summary>
        Flush,

        /// <summary>The head was driven too deep into the wood (crushed fibres, weaker hold).</summary>
        Overdriven
    }

    /// <summary>
    /// Rules of thumb for screwing timber (D-038). Simplified gameplay rules inspired by common carpentry practice
    /// (pre-drill dense wood and near the ends of a board), not an engineering calculation.
    /// </summary>
    public static class FasteningRules
    {
        /// <summary>Wood denser than this (kg/m³) splits easily: always pre-drill.</summary>
        public const double PilotHoleDensityKgPerCubicMeter = 500.0;

        /// <summary>Without a pilot hole a screw needs this many diameters of distance from the board's end.</summary>
        public const int MinimumEndDistanceInDiameters = 10;

        /// <summary>Screws thicker than this (mm) always need a pilot hole.</summary>
        public const double PilotHoleDiameterMillimeters = 6.0;

        /// <summary>A head higher than this above the surface is proud.</summary>
        public static Length ProudLimit => Length.FromMillimeters(0.5);

        /// <summary>A head deeper than this below the surface is overdriven.</summary>
        public static Length OverdrivenLimit => Length.FromMillimeters(1);

        /// <summary>
        /// Whether a screw splits the board it goes through unless a pilot hole is drilled first.
        /// <paramref name="endDistance"/>: from the screw to the nearest end of that board (null = far from any end).
        /// </summary>
        public static bool NeedsPilotHole(FastenerSpec fastener, Length? endDistance, double densityKgPerCubicMeter)
        {
            if (fastener == null) throw new ArgumentNullException(nameof(fastener));

            if (densityKgPerCubicMeter > PilotHoleDensityKgPerCubicMeter ||
                fastener.Diameter.Millimeters > PilotHoleDiameterMillimeters)
            {
                return true;
            }

            return endDistance.HasValue && endDistance.Value < fastener.Diameter * MinimumEndDistanceInDiameters;
        }

        /// <param name="headHeight">Height of the head above the surface (negative = below).</param>
        public static ScrewSeat Seat(Length headHeight)
        {
            if (headHeight > ProudLimit)
            {
                return ScrewSeat.Proud;
            }

            return headHeight < -OverdrivenLimit ? ScrewSeat.Overdriven : ScrewSeat.Flush;
        }

        /// <summary>
        /// Clutch setting (1–20 scale) at which the drill-driver's clutch starts to slip when this screw's head reaches
        /// the surface. Larger and longer screws in denser wood need more torque. Gameplay scale, not newton-metres.
        /// </summary>
        public static double SeatingTorque(FastenerSpec fastener, double densityKgPerCubicMeter)
        {
            if (fastener == null) throw new ArgumentNullException(nameof(fastener));

            return 8.0 * (densityKgPerCubicMeter / 450.0) * (fastener.Diameter.Millimeters / 4.0) *
                   Math.Sqrt(fastener.Length.Millimeters / 40.0);
        }
    }

    /// <summary>
    /// Driving one screw with a drill-driver (D-038). While the trigger is held the screw advances; its resistance
    /// grows with depth and jumps when the head reaches the surface. The clutch slips (the screw stops) when the
    /// resistance exceeds its setting:
    /// <list type="bullet">
    /// <item>setting below 60 % of the seating torque: the screw stops part-way (proud);</item>
    /// <item>from 60 % up to the seating torque: it stops with the head flush (the right setting);</item>
    /// <item>at or above it, or in drill mode (no clutch): the head keeps sinking while the trigger is held (overdriven).</item>
    /// </list>
    /// Releasing before the head is flush leaves it proud; it can be driven further later.
    /// </summary>
    public sealed class ScrewDrive
    {
        public const int MinClutch = 1;
        public const int MaxClutch = 20;

        /// <summary>Clutch value meaning drill mode: the clutch never slips.</summary>
        public const int DrillMode = MaxClutch + 1;

        private const double BodyTorqueShare = 0.6;

        /// <summary>
        /// The clutch settings that stop this screw flush by itself (D-042): from 60 % of the seating torque up to the
        /// last whole setting below it. False if no setting on the 1–20 scale does (then none is recommended).
        /// </summary>
        public static bool TryFlushClutchRange(double seatingTorque, out int lowest, out int highest)
        {
            if (seatingTorque <= 0.0) throw new ArgumentOutOfRangeException(nameof(seatingTorque), "Torque must be positive.");

            // A setting slips at the head when it is at least the body share and below the seating torque.
            lowest = Math.Max(MinClutch, (int)Math.Ceiling(BodyTorqueShare * seatingTorque - 1e-9));
            highest = Math.Min(MaxClutch, (int)Math.Ceiling(seatingTorque - 1e-9) - 1);
            return lowest <= highest;
        }

        private readonly double seatingTorque;
        private readonly double driveMillimetersPerSecond;
        private readonly double sinkMillimetersPerSecond;
        private double depthMillimeters;

        /// <param name="startDepth">Depth already driven (continuing a proud screw); zero for a new screw.</param>
        public ScrewDrive(FastenerSpec fastener, double seatingTorque, Length startDepth = default,
            double driveMillimetersPerSecond = 50.0, double sinkMillimetersPerSecond = 8.0)
        {
            Fastener = fastener ?? throw new ArgumentNullException(nameof(fastener));
            if (seatingTorque <= 0.0 || driveMillimetersPerSecond <= 0.0 || sinkMillimetersPerSecond <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(seatingTorque), "Torque and speeds must be positive.");
            }

            this.seatingTorque = seatingTorque;
            this.driveMillimetersPerSecond = driveMillimetersPerSecond;
            this.sinkMillimetersPerSecond = sinkMillimetersPerSecond;
            depthMillimeters = Math.Max(0.0, startDepth.Millimeters);
        }

        public FastenerSpec Fastener { get; }

        /// <summary>How far the screw is in (the head is flush at the screw's length).</summary>
        public Length Depth => Length.FromMillimeters(depthMillimeters);

        /// <summary>Height of the head above the surface (negative = below).</summary>
        public Length HeadHeight => Fastener.Length - Depth;

        public ScrewSeat Seat => FasteningRules.Seat(HeadHeight);

        /// <summary>True when the last <see cref="Advance"/> made no progress because the clutch slipped.</summary>
        public bool IsSlipping { get; private set; }

        /// <summary>Deepest the head can be driven below the surface.</summary>
        public static Length MaximumSink => Length.FromMillimeters(6);

        /// <summary>Drives for <paramref name="seconds"/> with the trigger held at the given clutch setting.</summary>
        public void Advance(double seconds, int clutch)
        {
            if (seconds < 0.0) throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Time must not be negative.");
            if (clutch < MinClutch || clutch > DrillMode)
            {
                throw new ArgumentOutOfRangeException(nameof(clutch), clutch, $"Clutch must be {MinClutch}..{DrillMode}.");
            }

            double limit = clutch == DrillMode ? double.PositiveInfinity : clutch;
            double length = Fastener.Length.Millimeters;
            double before = depthMillimeters;

            if (depthMillimeters < length)
            {
                // Body: resistance grows linearly up to 60 % of the seating torque.
                double reachable = limit >= BodyTorqueShare * seatingTorque
                    ? length
                    : length * limit / (BodyTorqueShare * seatingTorque);
                depthMillimeters = Math.Max(depthMillimeters, Math.Min(depthMillimeters + driveMillimetersPerSecond * seconds, reachable));
            }
            else if (limit >= seatingTorque)
            {
                // Head at the surface: it only sinks further if the clutch holds the full seating torque.
                depthMillimeters = Math.Min(depthMillimeters + sinkMillimetersPerSecond * seconds,
                    length + MaximumSink.Millimeters);
            }

            IsSlipping = seconds > 0.0 && depthMillimeters <= before;
        }
    }
}
