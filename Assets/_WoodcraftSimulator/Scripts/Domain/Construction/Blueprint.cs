using System;
using System.Collections.Generic;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Construction
{
    /// <summary>A fastener type, e.g. a 5 × 80 mm wood screw. Generic sizes only, no products (master prompt §6.8).</summary>
    public sealed class FastenerSpec
    {
        public FastenerSpec(string id, string displayName, Length diameter, Length length)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Fastener id must not be empty.", nameof(id));
            }

            if (diameter <= Length.Zero || length <= Length.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Fastener dimensions must be positive.");
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Diameter = diameter;
            Length = length;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public Length Diameter { get; }

        public Length Length { get; }

        public override string ToString() => DisplayName;
    }

    /// <summary>A place in a blueprint for one piece of timber with given dimensions.</summary>
    public sealed class PartSlot
    {
        public PartSlot(string id, string displayName, BoardDimensions dimensions, IEnumerable<string> prerequisites = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Slot id must not be empty.", nameof(id));
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Dimensions = dimensions;
            Prerequisites = new List<string>(prerequisites ?? Array.Empty<string>()).AsReadOnly();
        }

        public string Id { get; }

        public string DisplayName { get; }

        /// <summary>Nominal (target) dimensions of the piece.</summary>
        public BoardDimensions Dimensions { get; }

        /// <summary>Slots that must be filled before this one (assembly order).</summary>
        public IReadOnlyList<string> Prerequisites { get; }
    }

    /// <summary>A joint between two slots, made with a number of fasteners of one type (one per fastening point).</summary>
    public sealed class ConnectionSpec
    {
        /// <param name="endDistances">
        /// Optional, one per fastening point: distance from the point to the nearest end of <paramref name="partA"/>
        /// (the board the fastener goes through). Used for the pilot-hole rule; null = far from any end.
        /// </param>
        public ConnectionSpec(string id, string partA, string partB, string fastenerId, int fastenerCount,
            IEnumerable<Length> endDistances = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Connection id must not be empty.", nameof(id));
            }

            if (fastenerCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(fastenerCount), fastenerCount, "At least one fastener is needed.");
            }

            Id = id;
            PartA = partA ?? throw new ArgumentNullException(nameof(partA));
            PartB = partB ?? throw new ArgumentNullException(nameof(partB));
            FastenerId = fastenerId ?? throw new ArgumentNullException(nameof(fastenerId));
            FastenerCount = fastenerCount;

            if (endDistances != null)
            {
                var distances = new List<Length>(endDistances);
                if (distances.Count != fastenerCount)
                {
                    throw new ArgumentException("One end distance per fastening point is needed.", nameof(endDistances));
                }

                EndDistances = distances.AsReadOnly();
            }
        }

        /// <summary>Per fastening point: distance to the nearest end of <see cref="PartA"/>; null if not given.</summary>
        public IReadOnlyList<Length> EndDistances { get; }

        public Length? EndDistance(int point) =>
            EndDistances != null && point >= 0 && point < EndDistances.Count ? EndDistances[point] : (Length?)null;

        public string Id { get; }

        public string PartA { get; }

        public string PartB { get; }

        public string FastenerId { get; }

        public int FastenerCount { get; }
    }

    /// <summary>
    /// What to build: part slots, the connections between them and the dimensional tolerances.
    /// <see cref="AccuracyTolerance"/>: a piece within it is "accurate". <see cref="FitTolerance"/>: a piece within it
    /// still fits (reported as inaccurate); beyond it the piece doesn't fit at all.
    /// </summary>
    public sealed class Blueprint
    {
        private readonly Dictionary<string, PartSlot> slotsById = new Dictionary<string, PartSlot>();
        private readonly Dictionary<string, ConnectionSpec> connectionsById = new Dictionary<string, ConnectionSpec>();

        public Blueprint(string id, string displayName, IEnumerable<PartSlot> slots, IEnumerable<ConnectionSpec> connections,
            Tolerance accuracyTolerance, Tolerance fitTolerance)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Blueprint id must not be empty.", nameof(id));
            }

            if (fitTolerance.Allowed < accuracyTolerance.Allowed)
            {
                throw new ArgumentException("The fit tolerance must not be smaller than the accuracy tolerance.", nameof(fitTolerance));
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            AccuracyTolerance = accuracyTolerance;
            FitTolerance = fitTolerance;

            var slotList = new List<PartSlot>();
            foreach (PartSlot slot in slots ?? throw new ArgumentNullException(nameof(slots)))
            {
                if (slot == null || !slotsById.TryAdd(slot.Id, slot))
                {
                    throw new ArgumentException($"Duplicate or null slot '{slot?.Id}'.", nameof(slots));
                }

                slotList.Add(slot);
            }

            if (slotList.Count == 0)
            {
                throw new ArgumentException("A blueprint needs at least one slot.", nameof(slots));
            }

            foreach (PartSlot slot in slotList)
            {
                foreach (string prerequisite in slot.Prerequisites)
                {
                    if (prerequisite == slot.Id || !slotsById.ContainsKey(prerequisite))
                    {
                        throw new ArgumentException($"Slot '{slot.Id}' has an invalid prerequisite '{prerequisite}'.", nameof(slots));
                    }
                }
            }

            var connectionList = new List<ConnectionSpec>();
            foreach (ConnectionSpec connection in connections ?? throw new ArgumentNullException(nameof(connections)))
            {
                if (connection == null || !connectionsById.TryAdd(connection.Id, connection))
                {
                    throw new ArgumentException($"Duplicate or null connection '{connection?.Id}'.", nameof(connections));
                }

                if (!slotsById.ContainsKey(connection.PartA) || !slotsById.ContainsKey(connection.PartB) ||
                    connection.PartA == connection.PartB)
                {
                    throw new ArgumentException($"Connection '{connection.Id}' must join two different existing slots.", nameof(connections));
                }

                connectionList.Add(connection);
            }

            Slots = slotList.AsReadOnly();
            Connections = connectionList.AsReadOnly();
        }

        public string Id { get; }

        public string DisplayName { get; }

        public IReadOnlyList<PartSlot> Slots { get; }

        public IReadOnlyList<ConnectionSpec> Connections { get; }

        public Tolerance AccuracyTolerance { get; }

        public Tolerance FitTolerance { get; }

        public bool TryGetSlot(string id, out PartSlot slot) => slotsById.TryGetValue(id ?? string.Empty, out slot);

        public bool TryGetConnection(string id, out ConnectionSpec connection) =>
            connectionsById.TryGetValue(id ?? string.Empty, out connection);
    }
}
