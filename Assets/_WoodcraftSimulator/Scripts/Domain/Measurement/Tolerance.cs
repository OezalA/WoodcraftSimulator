using System;

namespace WoodcraftSimulator.Measurement
{
    /// <summary>Result of comparing an actual length with a target length.</summary>
    public readonly struct ToleranceCheck
    {
        public ToleranceCheck(Length target, Length actual, Length allowed)
        {
            Target = target;
            Actual = actual;
            Allowed = allowed;
        }

        public Length Target { get; }

        public Length Actual { get; }

        public Length Allowed { get; }

        /// <summary>Actual minus target: negative = too short, positive = too long.</summary>
        public Length Deviation => Actual - Target;

        /// <summary>True when |deviation| ≤ allowed (the limit itself still passes).</summary>
        public bool IsWithin => Deviation.Abs() <= Allowed;
    }

    /// <summary>A symmetric dimensional tolerance (± <see cref="Allowed"/>), e.g. ±1 mm for rough carpentry.</summary>
    public readonly struct Tolerance
    {
        public Tolerance(Length allowed)
        {
            if (allowed < Length.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(allowed), allowed, "Tolerance must not be negative.");
            }

            Allowed = allowed;
        }

        /// <summary>Default for marking and cutting in this prototype: ±1 mm.</summary>
        public static Tolerance Default => new Tolerance(Length.FromMillimeters(1));

        public Length Allowed { get; }

        public ToleranceCheck Check(Length target, Length actual) => new ToleranceCheck(target, actual, Allowed);

        /// <summary>e.g. "±1 mm".</summary>
        public override string ToString() => "±" + Allowed;
    }
}
