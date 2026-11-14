using System;
using System.Collections.Generic;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Construction
{
    public enum PlaceResult
    {
        Placed,
        UnknownSlot,
        SlotFilled,
        PrerequisiteMissing,
        DoesNotFit
    }

    public enum RemoveResult
    {
        Removed,
        UnknownSlot,
        SlotEmpty,

        /// <summary>A connection of this part already has fasteners in it.</summary>
        Fastened
    }

    public enum FastenResult
    {
        Fastened,
        UnknownConnection,
        InvalidPoint,
        PointAlreadyFastened,
        PartsMissing,
        WrongFastener
    }

    /// <summary>How a piece compares with a slot's nominal dimensions (actual minus nominal, per dimension).</summary>
    public readonly struct SlotFit
    {
        public SlotFit(ToleranceCheck length, ToleranceCheck width, ToleranceCheck thickness, Tolerance accuracy, Tolerance fit)
        {
            Length = length;
            Width = width;
            Thickness = thickness;
            IsAccurate = Within(length, accuracy) && Within(width, accuracy) && Within(thickness, accuracy);
            Fits = Within(length, fit) && Within(width, fit) && Within(thickness, fit);
        }

        public ToleranceCheck Length { get; }

        public ToleranceCheck Width { get; }

        public ToleranceCheck Thickness { get; }

        /// <summary>All dimensions within the accuracy tolerance.</summary>
        public bool IsAccurate { get; }

        /// <summary>All dimensions within the fit tolerance (the piece can be installed).</summary>
        public bool Fits { get; }

        private static bool Within(ToleranceCheck check, Tolerance tolerance) =>
            check.Deviation.Abs() <= tolerance.Allowed;
    }

    /// <summary>One line of the quality report: a slot and the piece in it (if any).</summary>
    public sealed class PartQuality
    {
        internal PartQuality(PartSlot slot, TimberPiece piece, SlotFit? fit)
        {
            Slot = slot;
            Piece = piece;
            Fit = fit;
        }

        public PartSlot Slot { get; }

        /// <summary>The installed piece, or null if the slot is empty.</summary>
        public TimberPiece Piece { get; }

        public SlotFit? Fit { get; }

        public bool IsInstalled => Piece != null;

        public bool IsAccurate => Fit.HasValue && Fit.Value.IsAccurate;
    }

    /// <summary>
    /// Quality of an assembly as separate, explained components (master prompt §6.12: no unexplained single score).
    /// It describes this build only and certifies nothing about structural safety.
    /// </summary>
    public sealed class AssemblyQualityReport
    {
        internal AssemblyQualityReport(IReadOnlyList<PartQuality> parts, int fastenersInstalled, int fastenersRequired)
        {
            Parts = parts;
            FastenersInstalled = fastenersInstalled;
            FastenersRequired = fastenersRequired;

            foreach (PartQuality part in parts)
            {
                if (part.IsInstalled)
                {
                    PartsInstalled++;
                    if (part.IsAccurate)
                    {
                        AccurateParts++;
                    }
                }
            }
        }

        public IReadOnlyList<PartQuality> Parts { get; }

        public int PartsInstalled { get; }

        public int PartsRequired => Parts.Count;

        /// <summary>Installed parts within the accuracy tolerance.</summary>
        public int AccurateParts { get; }

        public int FastenersInstalled { get; }

        public int FastenersRequired { get; }

        public bool IsComplete => PartsInstalled == PartsRequired && FastenersInstalled == FastenersRequired;
    }

    /// <summary>
    /// The state of one assembly being built from a <see cref="Blueprint"/>: which piece is in which slot and which
    /// fastening points are done. Rules: a piece must fit its slot; prerequisites first; fasteners only when both parts
    /// are in place and of the required type; a part can't be removed once one of its connections is fastened.
    /// </summary>
    public sealed class AssemblyBuild
    {
        private readonly Dictionary<string, TimberPiece> installed = new Dictionary<string, TimberPiece>();
        private readonly Dictionary<string, bool[]> fastened = new Dictionary<string, bool[]>();

        public AssemblyBuild(Blueprint blueprint)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            foreach (ConnectionSpec connection in blueprint.Connections)
            {
                fastened[connection.Id] = new bool[connection.FastenerCount];
            }
        }

        public Blueprint Blueprint { get; }

        public bool IsComplete => CreateReport().IsComplete;

        /// <summary>Raised after a piece was placed or removed, or a fastener was driven.</summary>
        public event Action Changed;

        /// <summary>Raised once, when the last missing step completes the assembly.</summary>
        public event Action Completed;

        public TimberPiece GetPiece(string slotId) => installed.TryGetValue(slotId ?? string.Empty, out TimberPiece piece) ? piece : null;

        public bool IsFastened(string connectionId, int point) =>
            fastened.TryGetValue(connectionId ?? string.Empty, out bool[] points) && point >= 0 && point < points.Length && points[point];

        /// <summary>Compares a piece with a slot without placing it (e.g. for HUD feedback).</summary>
        public SlotFit CheckFit(PartSlot slot, TimberPiece piece)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            if (piece == null) throw new ArgumentNullException(nameof(piece));

            BoardDimensions nominal = slot.Dimensions;
            BoardDimensions actual = piece.Dimensions;
            Tolerance accuracy = Blueprint.AccuracyTolerance;
            return new SlotFit(
                accuracy.Check(nominal.Length, actual.Length),
                accuracy.Check(nominal.Width, actual.Width),
                accuracy.Check(nominal.Thickness, actual.Thickness),
                accuracy,
                Blueprint.FitTolerance);
        }

        /// <summary>Why the piece can't go into the slot right now, or <see cref="PlaceResult.Placed"/> if it can.</summary>
        public PlaceResult CanPlace(string slotId, TimberPiece piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));

            if (!Blueprint.TryGetSlot(slotId, out PartSlot slot))
            {
                return PlaceResult.UnknownSlot;
            }

            if (installed.ContainsKey(slotId))
            {
                return PlaceResult.SlotFilled;
            }

            foreach (string prerequisite in slot.Prerequisites)
            {
                if (!installed.ContainsKey(prerequisite))
                {
                    return PlaceResult.PrerequisiteMissing;
                }
            }

            return CheckFit(slot, piece).Fits ? PlaceResult.Placed : PlaceResult.DoesNotFit;
        }

        public PlaceResult TryPlace(string slotId, TimberPiece piece)
        {
            PlaceResult result = CanPlace(slotId, piece);
            if (result == PlaceResult.Placed)
            {
                installed[slotId] = piece;
                NotifyChanged(wasComplete: false);
            }

            return result;
        }

        public RemoveResult TryRemove(string slotId, out TimberPiece piece)
        {
            piece = null;
            if (!Blueprint.TryGetSlot(slotId, out _))
            {
                return RemoveResult.UnknownSlot;
            }

            if (!installed.TryGetValue(slotId, out TimberPiece current))
            {
                return RemoveResult.SlotEmpty;
            }

            foreach (ConnectionSpec connection in Blueprint.Connections)
            {
                if ((connection.PartA == slotId || connection.PartB == slotId) && Array.IndexOf(fastened[connection.Id], true) >= 0)
                {
                    return RemoveResult.Fastened;
                }
            }

            installed.Remove(slotId);
            piece = current;
            NotifyChanged(wasComplete: false);
            return RemoveResult.Removed;
        }

        /// <summary>Drives one fastener into one fastening point of a connection.</summary>
        public FastenResult TryFasten(string connectionId, int point, FastenerSpec fastener)
        {
            if (fastener == null) throw new ArgumentNullException(nameof(fastener));

            if (!Blueprint.TryGetConnection(connectionId, out ConnectionSpec connection))
            {
                return FastenResult.UnknownConnection;
            }

            bool[] points = fastened[connection.Id];
            if (point < 0 || point >= points.Length)
            {
                return FastenResult.InvalidPoint;
            }

            if (points[point])
            {
                return FastenResult.PointAlreadyFastened;
            }

            if (!installed.ContainsKey(connection.PartA) || !installed.ContainsKey(connection.PartB))
            {
                return FastenResult.PartsMissing;
            }

            if (fastener.Id != connection.FastenerId)
            {
                return FastenResult.WrongFastener;
            }

            bool wasComplete = IsComplete;
            points[point] = true;
            NotifyChanged(wasComplete);
            return FastenResult.Fastened;
        }

        public AssemblyQualityReport CreateReport()
        {
            var parts = new List<PartQuality>();
            foreach (PartSlot slot in Blueprint.Slots)
            {
                TimberPiece piece = GetPiece(slot.Id);
                parts.Add(new PartQuality(slot, piece, piece != null ? CheckFit(slot, piece) : (SlotFit?)null));
            }

            int required = 0;
            int done = 0;
            foreach (bool[] points in fastened.Values)
            {
                required += points.Length;
                foreach (bool point in points)
                {
                    if (point)
                    {
                        done++;
                    }
                }
            }

            return new AssemblyQualityReport(parts.AsReadOnly(), done, required);
        }

        private void NotifyChanged(bool wasComplete)
        {
            Changed?.Invoke();
            if (!wasComplete && IsComplete)
            {
                Completed?.Invoke();
            }
        }
    }
}
