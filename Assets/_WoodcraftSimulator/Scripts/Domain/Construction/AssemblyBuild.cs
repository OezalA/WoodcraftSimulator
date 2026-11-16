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
        DoesNotFit,

        /// <summary>A post this part rests on is not plumbed and braced yet (D-049).</summary>
        PrerequisiteNotBraced
    }

    public enum RemoveResult
    {
        Removed,
        UnknownSlot,
        SlotEmpty,

        /// <summary>A connection of this part already has fasteners (or pilot holes) in it.</summary>
        Fastened
    }

    public enum FastenResult
    {
        Fastened,
        UnknownConnection,
        InvalidPoint,
        PointAlreadyFastened,
        PartsMissing,
        WrongFastener,

        /// <summary>Only a fastened screw whose head still stands out can be driven further, and only deeper.</summary>
        NotProud,

        /// <summary>A dowel goes into a drilled hole only (D-045).</summary>
        HoleRequired,

        /// <summary>A post of this joint is not plumbed and braced yet (D-049).</summary>
        NotBraced
    }

    public enum PilotResult
    {
        Drilled,
        UnknownConnection,
        InvalidPoint,
        AlreadyDrilled,
        AlreadyFastened,
        PartsMissing
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

    /// <summary>A driven fastener in the quality report.</summary>
    public sealed class FastenerQuality
    {
        internal FastenerQuality(ConnectionSpec connection, int point, Length headHeight, bool pilotHole, bool split)
        {
            Connection = connection;
            Point = point;
            HeadHeight = headHeight;
            PilotHole = pilotHole;
            Split = split;
        }

        public ConnectionSpec Connection { get; }

        public int Point { get; }

        /// <summary>Height of the head above the surface (negative = below).</summary>
        public Length HeadHeight { get; }

        public ScrewSeat Seat => FasteningRules.Seat(HeadHeight);

        public bool PilotHole { get; }

        /// <summary>The board split at this screw (no pilot hole where one was needed).</summary>
        public bool Split { get; }
    }

    /// <summary>
    /// Quality of an assembly as separate, explained components (master prompt §6.12: no unexplained single score).
    /// It describes this build only and certifies nothing about structural safety.
    /// </summary>
    public sealed class AssemblyQualityReport
    {
        internal AssemblyQualityReport(IReadOnlyList<PartQuality> parts, IReadOnlyList<FastenerQuality> fasteners,
            int fastenersRequired, int dryingJoints = 0)
        {
            DryingJoints = dryingJoints;
            Parts = parts;
            Fasteners = fasteners;
            FastenersInstalled = fasteners.Count;
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

            foreach (FastenerQuality fastener in fasteners)
            {
                switch (fastener.Seat)
                {
                    case ScrewSeat.Flush:
                        FlushFasteners++;
                        break;
                    case ScrewSeat.Proud:
                        ProudFasteners++;
                        break;
                    default:
                        OverdrivenFasteners++;
                        break;
                }

                if (fastener.Split)
                {
                    Splits++;
                }
            }
        }

        public IReadOnlyList<PartQuality> Parts { get; }

        /// <summary>Every driven fastener, in blueprint order.</summary>
        public IReadOnlyList<FastenerQuality> Fasteners { get; }

        public int PartsInstalled { get; }

        public int PartsRequired => Parts.Count;

        /// <summary>Installed parts within the accuracy tolerance.</summary>
        public int AccurateParts { get; }

        public int FastenersInstalled { get; }

        public int FastenersRequired { get; }

        public int FlushFasteners { get; }

        public int ProudFasteners { get; }

        public int OverdrivenFasteners { get; }

        /// <summary>Screws that split the board (no pilot hole where one was needed).</summary>
        public int Splits { get; }

        /// <summary>Everything installed and fastened. Quality (accuracy, seats, splits) is reported separately.</summary>
        /// <summary>Glued joints whose glue is still drying (D-045).</summary>
        public int DryingJoints { get; }

        public bool IsComplete => PartsInstalled == PartsRequired && FastenersInstalled == FastenersRequired && DryingJoints == 0;
    }

    /// <summary>
    /// The state of one assembly being built from a <see cref="Blueprint"/>: which piece is in which slot and, per
    /// fastening point, pilot hole and driven screw. Rules: a piece must fit its slot; prerequisites first; drilling and
    /// fastening only when both parts are in place, with the required fastener type; a part can't be removed once a
    /// connection of it is drilled or fastened. A screw without a pilot hole where one is needed splits the board
    /// (<see cref="FasteningRules.NeedsPilotHole"/>, D-038): it still holds, but the report lists the split.
    /// </summary>
    public sealed class AssemblyBuild
    {
        private sealed class PointState
        {
            public bool PilotHole;
            public bool Fastened;
            public Length HeadHeight;
            public bool Split;
        }

        private readonly Dictionary<string, TimberPiece> installed = new Dictionary<string, TimberPiece>();
        private readonly Dictionary<string, PointState[]> points = new Dictionary<string, PointState[]>();
        private readonly Dictionary<string, double> drying = new Dictionary<string, double>();
        private readonly Dictionary<string, PostLean> leans = new Dictionary<string, PostLean>();
        private readonly Func<double> random;

        /// <param name="random">
        /// Source of random numbers in [0, 1) for the lean of a freshly set post (D-049); null = a new System.Random.
        /// </param>
        public AssemblyBuild(Blueprint blueprint, Func<double> random = null)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            if (random == null)
            {
                var generator = new Random();
                random = generator.NextDouble;
            }

            this.random = random;
            foreach (ConnectionSpec connection in blueprint.Connections)
            {
                var states = new PointState[connection.FastenerCount];
                for (int i = 0; i < states.Length; i++)
                {
                    states[i] = new PointState();
                }

                points[connection.Id] = states;
            }
        }

        public Blueprint Blueprint { get; }

        public bool IsComplete => CreateReport().IsComplete;

        /// <summary>Raised after a piece was placed or removed, a hole drilled or a fastener driven.</summary>
        public event Action Changed;

        /// <summary>Raised once, when the last missing step completes the assembly.</summary>
        public event Action Completed;

        public TimberPiece GetPiece(string slotId) => installed.TryGetValue(slotId ?? string.Empty, out TimberPiece piece) ? piece : null;

        public bool IsFastened(string connectionId, int point) => TryGetPoint(connectionId, point, out PointState state) && state.Fastened;

        public bool HasPilotHole(string connectionId, int point) => TryGetPoint(connectionId, point, out PointState state) && state.PilotHole;

        public bool IsSplit(string connectionId, int point) => TryGetPoint(connectionId, point, out PointState state) && state.Split;

        /// <summary>Seconds the glue of this joint still needs to dry (0 = dry or not glued).</summary>
        public double DryingSeconds(string connectionId) =>
            drying.TryGetValue(connectionId ?? string.Empty, out double seconds) ? seconds : 0.0;

        public bool IsDrying(string connectionId) => DryingSeconds(connectionId) > 0.0;

        /// <summary>Lets glue dry for <paramref name="seconds"/> of playing time. Completes the assembly when the last joint is dry.</summary>
        public void AdvanceTime(double seconds)
        {
            if (seconds <= 0.0 || drying.Count == 0)
            {
                return;
            }

            bool wasComplete = IsComplete;
            bool anyDried = false;
            foreach (string id in new List<string>(drying.Keys))
            {
                double left = drying[id] - seconds;
                if (left <= 0.0)
                {
                    drying.Remove(id);
                    anyDried = true;
                }
                else
                {
                    drying[id] = left;
                }
            }

            if (anyDried)
            {
                NotifyChanged(wasComplete);
            }
        }

        /// <summary>Loading a save: glue that was still drying.</summary>
        public void RestoreDrying(string connectionId, double seconds)
        {
            if (Blueprint.TryGetConnection(connectionId, out _) && seconds > 0.0)
            {
                drying[connectionId] = seconds;
            }
        }

        /// <summary>Head height of the screw in the point, or null if there is none.</summary>
        public Length? HeadHeight(string connectionId, int point) =>
            TryGetPoint(connectionId, point, out PointState state) && state.Fastened ? state.HeadHeight : (Length?)null;

        /// <summary>
        /// Whether a screw at this point needs a pilot hole with the pieces now installed (the denser of the two
        /// counts). False while a part is missing.
        /// </summary>
        public bool NeedsPilotHole(string connectionId, int point, FastenerSpec fastener)
        {
            // Dowels go into drilled holes anyway; steel connectors spread the load (D-046): neither splits.
            if (fastener == null || fastener.Kind == FastenerKind.Dowel || fastener.Kind == FastenerKind.Connector ||
                !Blueprint.TryGetConnection(connectionId, out ConnectionSpec connection) ||
                !installed.TryGetValue(connection.PartA, out TimberPiece a) ||
                !installed.TryGetValue(connection.PartB, out TimberPiece b))
            {
                return false;
            }

            double density = TimberDensity(a, b);
            return density > 0.0 && FasteningRules.NeedsPilotHole(fastener, connection.EndDistance(point), density);
        }

        /// <summary>Seating torque (clutch scale) for a screw at this point, from the denser installed piece.</summary>
        public double SeatingTorque(string connectionId, FastenerSpec fastener)
        {
            double density = 450.0;
            if (Blueprint.TryGetConnection(connectionId, out ConnectionSpec connection))
            {
                density = TimberDensity(GetPiece(connection.PartA), GetPiece(connection.PartB));
            }

            return FasteningRules.SeatingTorque(fastener, density > 0.0 ? density : 450.0);
        }

        /// <summary>The denser of the two pieces that are timber (concrete etc. is ignored); 0 if none is.</summary>
        private static double TimberDensity(TimberPiece a, TimberPiece b)
        {
            double density = 0.0;
            foreach (TimberPiece piece in new[] { a, b })
            {
                if (piece != null && piece.Species.IsTimber)
                {
                    density = Math.Max(density, piece.Species.DensityKgPerCubicMeter);
                }
            }

            return density;
        }

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

            if (!CheckFit(slot, piece).Fits)
            {
                return PlaceResult.DoesNotFit;
            }

            foreach (string prerequisite in slot.Prerequisites)
            {
                if (leans.TryGetValue(prerequisite, out PostLean lean) && !lean.Braced)
                {
                    return PlaceResult.PrerequisiteNotBraced;
                }
            }

            return PlaceResult.Placed;
        }

        public PlaceResult TryPlace(string slotId, TimberPiece piece)
        {
            PlaceResult result = CanPlace(slotId, piece);
            if (result == PlaceResult.Placed)
            {
                installed[slotId] = piece;
                Blueprint.TryGetSlot(slotId, out PartSlot slot);
                if (slot.RequiresPlumb)
                {
                    // A post never goes in exactly vertical (D-049).
                    leans[slotId] = new PostLean(PlumbRules.InitialLean(random()), PlumbRules.InitialLean(random()), false);
                }

                NotifyChanged(wasComplete: false);
            }

            return result;
        }

        /// <summary>Whether the slot's piece could be removed now, without removing it.</summary>
        public RemoveResult CanRemove(string slotId)
        {
            if (!Blueprint.TryGetSlot(slotId, out _))
            {
                return RemoveResult.UnknownSlot;
            }

            if (!installed.ContainsKey(slotId))
            {
                return RemoveResult.SlotEmpty;
            }

            foreach (ConnectionSpec connection in Blueprint.Connections)
            {
                if (connection.PartA != slotId && connection.PartB != slotId)
                {
                    continue;
                }

                foreach (PointState state in points[connection.Id])
                {
                    if (state.Fastened || state.PilotHole)
                    {
                        return RemoveResult.Fastened;
                    }
                }
            }

            return RemoveResult.Removed;
        }

        public RemoveResult TryRemove(string slotId, out TimberPiece piece)
        {
            piece = null;
            RemoveResult result = CanRemove(slotId);
            if (result != RemoveResult.Removed)
            {
                return result;
            }

            piece = installed[slotId];
            installed.Remove(slotId);
            leans.Remove(slotId);
            NotifyChanged(wasComplete: false);
            return RemoveResult.Removed;
        }

        /// <summary>Drills a pilot hole at a fastening point (through part A into part B).</summary>
        public PilotResult TryDrillPilot(string connectionId, int point)
        {
            if (!Blueprint.TryGetConnection(connectionId, out ConnectionSpec connection))
            {
                return PilotResult.UnknownConnection;
            }

            if (!TryGetPoint(connectionId, point, out PointState state))
            {
                return PilotResult.InvalidPoint;
            }

            if (state.Fastened)
            {
                return PilotResult.AlreadyFastened;
            }

            if (state.PilotHole)
            {
                return PilotResult.AlreadyDrilled;
            }

            if (!installed.ContainsKey(connection.PartA) || !installed.ContainsKey(connection.PartB))
            {
                return PilotResult.PartsMissing;
            }

            state.PilotHole = true;
            NotifyChanged(wasComplete: false);
            return PilotResult.Drilled;
        }

        /// <summary>Drives one fastener with its head flush.</summary>
        public FastenResult TryFasten(string connectionId, int point, FastenerSpec fastener) =>
            TryFasten(connectionId, point, fastener, Length.Zero);

        /// <summary>Drives one fastener into one fastening point; <paramref name="headHeight"/> is where its head ended up.</summary>
        public FastenResult TryFasten(string connectionId, int point, FastenerSpec fastener, Length headHeight)
        {
            if (fastener == null) throw new ArgumentNullException(nameof(fastener));

            return Fasten(connectionId, point, fastener, headHeight, checkType: true);
        }

        /// <summary>Drives a proud screw further: the head may only go deeper.</summary>
        public FastenResult TryDriveFurther(string connectionId, int point, Length headHeight)
        {
            if (!Blueprint.TryGetConnection(connectionId, out _))
            {
                return FastenResult.UnknownConnection;
            }

            if (!TryGetPoint(connectionId, point, out PointState state) || !state.Fastened)
            {
                return FastenResult.InvalidPoint;
            }

            if (FasteningRules.Seat(state.HeadHeight) != ScrewSeat.Proud || headHeight >= state.HeadHeight)
            {
                return FastenResult.NotProud;
            }

            state.HeadHeight = headHeight;
            NotifyChanged(wasComplete: true);
            return FastenResult.Fastened;
        }

        /// <summary>
        /// Loading a save: marks a point as fastened with the same rules as <see cref="TryFasten(string, int, FastenerSpec, Length)"/>
        /// except the fastener type, which was checked when it was driven (the save doesn't store it). The split is
        /// recomputed from the rules when <paramref name="fastener"/> (the connection's type) is given.
        /// </summary>
        public FastenResult RestoreFastened(string connectionId, int point, Length headHeight = default,
            FastenerSpec fastener = null) =>
            Fasten(connectionId, point, fastener, headHeight, checkType: false);

        /// <summary>Loading a save: a pilot hole that was drilled.</summary>
        public PilotResult RestorePilot(string connectionId, int point) => TryDrillPilot(connectionId, point);

        private FastenResult Fasten(string connectionId, int point, FastenerSpec fastener, Length headHeight, bool checkType)
        {
            if (!Blueprint.TryGetConnection(connectionId, out ConnectionSpec connection))
            {
                return FastenResult.UnknownConnection;
            }

            if (!TryGetPoint(connectionId, point, out PointState state))
            {
                return FastenResult.InvalidPoint;
            }

            if (state.Fastened)
            {
                return FastenResult.PointAlreadyFastened;
            }

            if (!installed.ContainsKey(connection.PartA) || !installed.ContainsKey(connection.PartB))
            {
                return FastenResult.PartsMissing;
            }

            if (checkType && fastener.Id != connection.FastenerId)
            {
                return FastenResult.WrongFastener;
            }

            if (checkType && UnbracedPost(connectionId) != null)
            {
                return FastenResult.NotBraced;
            }

            bool dowel = fastener != null && fastener.Kind == FastenerKind.Dowel;
            if (checkType && dowel && !state.PilotHole)
            {
                return FastenResult.HoleRequired;
            }

            bool wasComplete = IsComplete;
            state.Fastened = true;
            state.HeadHeight = headHeight;
            state.Split = !dowel && !state.PilotHole && fastener != null && NeedsPilotHole(connectionId, point, fastener);

            // The last dowel of a glued joint: the clamps go on and the glue starts drying (not when loading a save).
            if (dowel && checkType && Array.TrueForAll(points[connectionId], p => p.Fastened))
            {
                drying[connectionId] = GlueRules.DryingSeconds;
            }

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
            var fasteners = new List<FastenerQuality>();
            foreach (ConnectionSpec connection in Blueprint.Connections)
            {
                PointState[] states = points[connection.Id];
                required += states.Length;
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i].Fastened)
                    {
                        fasteners.Add(new FastenerQuality(connection, i, states[i].HeadHeight, states[i].PilotHole, states[i].Split));
                    }
                }
            }

            return new AssemblyQualityReport(parts.AsReadOnly(), fasteners.AsReadOnly(), required, drying.Count);
        }

        private bool TryGetPoint(string connectionId, int point, out PointState state)
        {
            state = null;
            if (!points.TryGetValue(connectionId ?? string.Empty, out PointState[] states) || point < 0 || point >= states.Length)
            {
                return false;
            }

            state = states[point];
            return true;
        }

        /// <summary>The lean of an installed post (D-049); false for an empty slot or a slot that is not a post.</summary>
        public bool TryGetLean(string slotId, out PostLean lean) => leans.TryGetValue(slotId ?? string.Empty, out lean);

        /// <summary>
        /// Pushes an unbraced post: changes its lean along one direction by <paramref name="deltaTenths"/> tenths of a
        /// degree (limited to <see cref="PlumbRules.MaxLeanTenths"/>).
        /// </summary>
        public PlumbResult TryAdjustLean(string slotId, LeanAxis axis, int deltaTenths)
        {
            PlumbResult check = CheckPost(slotId, out PostLean lean);
            if (check != PlumbResult.Done)
            {
                return check;
            }

            if (lean.Braced)
            {
                return PlumbResult.Braced;
            }

            int x = lean.XTenths;
            int z = lean.ZTenths;
            if (axis == LeanAxis.X)
            {
                x = Clamp(x + deltaTenths);
            }
            else
            {
                z = Clamp(z + deltaTenths);
            }

            leans[slotId] = new PostLean(x, z, false);
            NotifyChanged(wasComplete: true);
            return PlumbResult.Done;
        }

        /// <summary>Fixes a plumb post with a temporary brace; from now on its joints may be fastened.</summary>
        public PlumbResult TryBrace(string slotId)
        {
            PlumbResult check = CheckPost(slotId, out PostLean lean);
            if (check != PlumbResult.Done)
            {
                return check;
            }

            if (lean.Braced)
            {
                return PlumbResult.Braced;
            }

            if (!lean.IsPlumb)
            {
                return PlumbResult.NotPlumb;
            }

            leans[slotId] = new PostLean(lean.XTenths, lean.ZTenths, true);
            NotifyChanged(wasComplete: true);
            return PlumbResult.Done;
        }

        /// <summary>Loading a save: the saved lean and brace of an installed post.</summary>
        public void RestoreLean(string slotId, int xTenths, int zTenths, bool braced)
        {
            if (CheckPost(slotId, out _) == PlumbResult.Done)
            {
                leans[slotId] = new PostLean(Clamp(xTenths), Clamp(zTenths), braced);
                Changed?.Invoke();
            }
        }

        /// <summary>The first installed post of this joint that is not braced yet, or null (D-049).</summary>
        public string UnbracedPost(string connectionId)
        {
            if (!Blueprint.TryGetConnection(connectionId, out ConnectionSpec connection))
            {
                return null;
            }

            foreach (string part in new[] { connection.PartA, connection.PartB })
            {
                if (leans.TryGetValue(part, out PostLean lean) && !lean.Braced)
                {
                    return part;
                }
            }

            return null;
        }

        private PlumbResult CheckPost(string slotId, out PostLean lean)
        {
            lean = default;
            if (!Blueprint.TryGetSlot(slotId, out PartSlot slot))
            {
                return PlumbResult.UnknownSlot;
            }

            if (!slot.RequiresPlumb)
            {
                return PlumbResult.NotAPost;
            }

            return leans.TryGetValue(slotId, out lean) ? PlumbResult.Done : PlumbResult.SlotEmpty;
        }

        private static int Clamp(int tenths) => Math.Max(-PlumbRules.MaxLeanTenths, Math.Min(PlumbRules.MaxLeanTenths, tenths));

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
