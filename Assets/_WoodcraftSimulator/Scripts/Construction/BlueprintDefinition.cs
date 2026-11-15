using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Construction
{
    /// <summary>
    /// A buildable structure as data (ScriptableObject asset): part slots with nominal dimensions and their pose in the
    /// assembly, and connections with their fastening points. Converted to the domain <see cref="Blueprint"/> for rules;
    /// the poses are presentation only.
    /// Slot pose convention: the piece's own axes (length = local Z, width = local X, thickness = local Y) as placed.
    /// </summary>
    [CreateAssetMenu(menuName = "WoodcraftSimulator/Blueprint", fileName = "Blueprint")]
    public sealed class BlueprintDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Slot
        {
            public string id = "part";
            public string displayName = "Part";

            [Tooltip("Turkish name (D-041); empty = the English name is shown.")]
            public string displayNameTurkish = string.Empty;

            [Min(1f)] public float lengthMillimeters = 600f;
            [Min(1f)] public float widthMillimeters = 95f;
            [Min(1f)] public float thicknessMillimeters = 45f;

            [Tooltip("Center of the piece relative to the assembly origin (meters).")]
            public Vector3 localPosition;

            public Vector3 localEulerAngles;

            [Tooltip("Ids of slots that must be filled first (assembly order). Optional.")]
            public string[] prerequisites = Array.Empty<string>();
        }

        [Serializable]
        public sealed class FasteningPoint
        {
            [Tooltip("Where the fastener enters, relative to the assembly origin (meters).")]
            public Vector3 localPosition;

            [Tooltip("Driving direction (into the material), relative to the assembly.")]
            public Vector3 localDirection = Vector3.down;
        }

        [Serializable]
        public sealed class Connection
        {
            public string id = "joint";
            public string partA = "part-a";
            public string partB = "part-b";
            public string fastenerId = "wood-screw-5x80";
            public FasteningPoint[] points = Array.Empty<FasteningPoint>();
        }

        [SerializeField] private string id = "blueprint";
        [SerializeField] private string displayName = "Blueprint";

        [Tooltip("Turkish name (D-041); empty = the English name is shown.")]
        [SerializeField] private string displayNameTurkish = string.Empty;

        [Tooltip("Deviation (mm) within which a part counts as accurate.")]
        [SerializeField, Min(0f)] private float accuracyMillimeters = 1f;

        [Tooltip("Deviation (mm) up to which a part still fits (reported as inaccurate).")]
        [SerializeField, Min(0f)] private float fitMillimeters = 5f;

        [SerializeField] private Slot[] slots = Array.Empty<Slot>();
        [SerializeField] private Connection[] connections = Array.Empty<Connection>();

        public string Id => id;

        public string DisplayName => Loc.Pick(displayName, displayNameTurkish);

        public IReadOnlyList<Slot> Slots => slots;

        public IReadOnlyList<Connection> Connections => connections;

        public Blueprint ToDomain()
        {
            var domainSlots = new List<PartSlot>();
            foreach (Slot slot in slots)
            {
                domainSlots.Add(new PartSlot(slot.id, Loc.Pick(slot.displayName, slot.displayNameTurkish),
                    BoardDimensions.FromMillimeters(slot.lengthMillimeters, slot.widthMillimeters, slot.thicknessMillimeters),
                    slot.prerequisites));
            }

            var domainConnections = new List<ConnectionSpec>();
            foreach (Connection connection in connections)
            {
                domainConnections.Add(new ConnectionSpec(connection.id, connection.partA, connection.partB,
                    connection.fastenerId, connection.points.Length, EndDistances(connection)));
            }

            return new Blueprint(id, DisplayName, domainSlots, domainConnections,
                new Tolerance(Length.FromMillimeters((double)accuracyMillimeters)),
                new Tolerance(Length.FromMillimeters((double)fitMillimeters)));
        }

        /// <summary>
        /// For each fastening point: distance along part A (the board the fastener goes through) from the point to that
        /// board's nearer end, from the slot pose. Null if part A is not a slot of this blueprint.
        /// </summary>
        private Length[] EndDistances(Connection connection)
        {
            Slot partA = Array.Find(slots, slot => slot.id == connection.partA);
            if (partA == null)
            {
                return null;
            }

            Quaternion rotation = Quaternion.Euler(partA.localEulerAngles);
            float halfLength = partA.lengthMillimeters / 2000f;
            var distances = new Length[connection.points.Length];
            for (int i = 0; i < distances.Length; i++)
            {
                // The board's length is its local Z axis.
                Vector3 local = Quaternion.Inverse(rotation) * (connection.points[i].localPosition - partA.localPosition);
                distances[i] = Length.FromMeters(Mathf.Max(0f, halfLength - Mathf.Abs(local.z)));
            }

            return distances;
        }
    }
}
