using System;
using System.Collections.Generic;
using UnityEngine;
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

        [Tooltip("Deviation (mm) within which a part counts as accurate.")]
        [SerializeField, Min(0f)] private float accuracyMillimeters = 1f;

        [Tooltip("Deviation (mm) up to which a part still fits (reported as inaccurate).")]
        [SerializeField, Min(0f)] private float fitMillimeters = 5f;

        [SerializeField] private Slot[] slots = Array.Empty<Slot>();
        [SerializeField] private Connection[] connections = Array.Empty<Connection>();

        public string Id => id;

        public string DisplayName => displayName;

        public IReadOnlyList<Slot> Slots => slots;

        public IReadOnlyList<Connection> Connections => connections;

        public Blueprint ToDomain()
        {
            var domainSlots = new List<PartSlot>();
            foreach (Slot slot in slots)
            {
                domainSlots.Add(new PartSlot(slot.id, slot.displayName,
                    BoardDimensions.FromMillimeters(slot.lengthMillimeters, slot.widthMillimeters, slot.thicknessMillimeters),
                    slot.prerequisites));
            }

            var domainConnections = new List<ConnectionSpec>();
            foreach (Connection connection in connections)
            {
                domainConnections.Add(new ConnectionSpec(connection.id, connection.partA, connection.partB,
                    connection.fastenerId, connection.points.Length));
            }

            return new Blueprint(id, displayName, domainSlots, domainConnections,
                new Tolerance(Length.FromMillimeters((double)accuracyMillimeters)),
                new Tolerance(Length.FromMillimeters((double)fitMillimeters)));
        }
    }
}
