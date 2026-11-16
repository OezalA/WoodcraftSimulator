using System;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Construction
{
    /// <summary>Horizontal direction of the assembly in which a post's top can lean (Phase 11, D-049).</summary>
    public enum LeanAxis
    {
        X,
        Z
    }

    public enum PlumbResult
    {
        Done,
        UnknownSlot,

        /// <summary>The slot is not a post that has to stand plumb.</summary>
        NotAPost,
        SlotEmpty,

        /// <summary>The post is braced: it can't be moved any more.</summary>
        Braced,

        /// <summary>Bracing needs the post plumb in both directions.</summary>
        NotPlumb
    }

    /// <summary>How far an installed post leans, in tenths of a degree per direction, and whether it is braced.</summary>
    public readonly struct PostLean
    {
        public PostLean(int xTenths, int zTenths, bool braced)
        {
            XTenths = xTenths;
            ZTenths = zTenths;
            Braced = braced;
        }

        /// <summary>Lean of the top towards +X, in tenths of a degree (negative = towards −X).</summary>
        public int XTenths { get; }

        /// <summary>Lean of the top towards +Z, in tenths of a degree.</summary>
        public int ZTenths { get; }

        public bool Braced { get; }

        public int Along(LeanAxis axis) => axis == LeanAxis.X ? XTenths : ZTenths;

        public bool IsPlumbAlong(LeanAxis axis) => PlumbRules.IsPlumb(Along(axis));

        public bool IsPlumb => PlumbRules.IsPlumb(XTenths) && PlumbRules.IsPlumb(ZTenths);
    }

    /// <summary>
    /// Plumbing a post (Phase 11, D-049). A post set into its base never stands exactly vertical: it leans 1–3° in
    /// each direction. The spirit level shows the lean of the face it is held against; the post is pushed upright and,
    /// once plumb in both directions, held with a temporary brace. Only a braced post may be fastened. The tolerance
    /// (0.5°, about 9 mm per metre) is a common rule of thumb for posts, not a standard.
    /// </summary>
    public static class PlumbRules
    {
        /// <summary>Plumb when the lean is at most this many tenths of a degree (0.5°).</summary>
        public const int ToleranceTenths = 5;

        /// <summary>A post can't be pushed further than 5° off vertical.</summary>
        public const int MaxLeanTenths = 50;

        /// <summary>Lean of a freshly set post per direction: 1.0° to 3.0°, either way.</summary>
        public const int MinInitialTenths = 10;
        public const int MaxInitialTenths = 30;

        public static bool IsPlumb(int leanTenths) => Math.Abs(leanTenths) <= ToleranceTenths;

        /// <summary>
        /// The initial lean for one direction from a random number in [0, 1): the lower half leans negative, the upper
        /// half positive, each spread evenly over 1.0°–3.0°.
        /// </summary>
        public static int InitialLean(double random)
        {
            double r = Math.Min(Math.Max(random, 0.0), 0.999999) * 2.0;
            int sign = r < 1.0 ? -1 : 1;
            double fraction = r < 1.0 ? r : r - 1.0;
            int magnitude = MinInitialTenths + (int)Math.Floor(fraction * (MaxInitialTenths - MinInitialTenths + 1));
            return sign * Math.Min(magnitude, MaxInitialTenths);
        }

        /// <summary>The post's offset in millimetres per metre of height (what a level's bubble shows).</summary>
        public static double MillimetersPerMeter(int leanTenths) => Math.Tan(leanTenths / 10.0 * Math.PI / 180.0) * 1000.0;

        /// <summary>Lean as text, e.g. "1.4°".</summary>
        public static string Format(int leanTenths) =>
            (Math.Abs(leanTenths) / 10.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "°";
    }

    /// <summary>
    /// How high a person works (Phase 11, D-049): fastening or setting a part higher than this above one's feet needs
    /// a ladder. About 2.1 m for an average adult (eye height ~1.65 m plus working arm reach); a simplification.
    /// </summary>
    public static class WorkReach
    {
        public static readonly Length MaxHeightAboveFeet = Length.FromMillimeters(2100);

        public static bool CanReach(Length heightAboveFeet) => heightAboveFeet <= MaxHeightAboveFeet;
    }
}
