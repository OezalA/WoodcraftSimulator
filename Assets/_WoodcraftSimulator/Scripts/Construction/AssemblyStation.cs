using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;
using WoodcraftSimulator.Persistence;

namespace WoodcraftSimulator.Construction
{
    /// <summary>Which fastening points <see cref="AssemblyStation.TryFindPoint"/> looks for.</summary>
    public enum PointQuery
    {
        /// <summary>Both parts installed, no screw and no pilot hole yet (for the drill bit).</summary>
        ForPilotHole,

        /// <summary>Both parts installed and no screw yet, or a screw whose head still stands out (for the driver bit).</summary>
        ForScrew
    }

    /// <summary>
    /// A place where one structure is built from a <see cref="BlueprintDefinition"/> (e.g. the assembly table).
    /// Shows translucent ghosts of the empty slots; a carried board aimed near a slot snaps into it when it fits
    /// (through <see cref="IPlacementTarget"/>, so the carrier knows nothing about assemblies). The rules are the
    /// domain <see cref="AssemblyBuild"/>. This transform is the assembly origin (slot poses are relative to it).
    /// The blueprint can be changed while the station is empty (a job brings its own, Phase 6).
    /// </summary>
    public sealed class AssemblyStation : MonoBehaviour, IPlacementTarget
    {
        [Tooltip("The blueprint built here until a job brings another one.")]
        [SerializeField] private BlueprintDefinition blueprint;

        [Tooltip("Every blueprint that may be built here (a save names its blueprint by id).")]
        [SerializeField] private BlueprintDefinition[] knownBlueprints = Array.Empty<BlueprintDefinition>();

        [Tooltip("Fastener types used by the blueprints (sizes for the pilot-hole rule).")]
        [SerializeField] private FastenerDefinition[] fasteners = Array.Empty<FastenerDefinition>();

        [Tooltip("Translucent material for the ghosts of empty slots.")]
        [SerializeField] private Material ghostMaterial;

        [Tooltip("A carried board snaps when the aim point is within this distance (m) of an empty slot.")]
        [SerializeField, Min(0.01f)] private float snapDistance = 0.08f;

        [Header("Fastening points")]
        [Tooltip("Marker of a fastening point that can be screwed now (both parts installed).")]
        [SerializeField] private Material pointMarkerMaterial;

        [Tooltip("Marker of a fastening point that needs a pilot hole first (near a board end, D-041).")]
        [SerializeField] private Material pilotPointMaterial;

        [Tooltip("Material of the visible screw heads.")]
        [SerializeField] private Material screwHeadMaterial;

        [Tooltip("Dark material for pilot holes and splits.")]
        [SerializeField] private Material holeMaterial;

        [Tooltip("Optional: the workshop's sawdust total, shown in the quality report.")]
        [SerializeField] private Cutting.WorkshopWaste waste;

        // Visual sizes (meters): an 8 mm marker square, a 4 mm pilot hole, a 9 mm screw head 1.5 mm high.
        private const float MarkerSize = 0.008f;
        private const float HoleSize = 0.004f;
        private const float ScrewHeadSize = 0.009f;
        private const float ScrewHeadHeight = 0.0015f;
        private const float SplitLength = 0.03f;

        private static Mesh unitCube;

        private readonly List<Mesh> ghostMeshes = new List<Mesh>();
        private readonly List<GameObject> visuals = new List<GameObject>();
        private Dictionary<string, FastenerSpec> fastenerSpecs;
        private BlueprintDefinition active;
        private GameObject[] ghosts;
        private Carryable[] installed;
        private Transform[] parentsBeforeInstall;
        private bool restoring;
        private GameObject[][] pointMarkers;
        private GameObject[][] holeMarkers;
        private GameObject[][] screwHeads;
        private GameObject drivingHead;

        public AssemblyBuild Build { get; private set; }

        /// <summary>The blueprint being built now.</summary>
        public BlueprintDefinition Definition => active;

        public string DisplayName => Build != null ? Build.Blueprint.DisplayName : name;

        /// <summary>No part installed (the blueprint may be changed).</summary>
        public bool IsEmpty => installed == null || Array.TrueForAll(installed, part => part == null);

        /// <summary>Raised after a part was installed or taken out, a hole drilled or a fastener driven.</summary>
        public event Action Changed;

        /// <summary>Raised once when the last part / fastener completes the structure.</summary>
        public event Action Completed;

        /// <summary>Raised after the blueprint changed.</summary>
        public event Action BlueprintChanged;

        private void Awake()
        {
            unitCube ??= BoxMeshBuilder.Create(Vector3.one, "Unit cube");
            fastenerSpecs = new Dictionary<string, FastenerSpec>();
            foreach (FastenerDefinition definition in fasteners)
            {
                if (definition != null)
                {
                    fastenerSpecs[definition.Id] = definition.ToDomain();
                }
            }

            if (blueprint == null)
            {
                Debug.LogError($"{nameof(AssemblyStation)} '{name}': blueprint is not assigned.", this);
                enabled = false;
                return;
            }

            Setup(blueprint);
        }

        /// <summary>The fastener type with this id, if the station knows it.</summary>
        public FastenerSpec Fastener(string fastenerId) =>
            fastenerSpecs != null && fastenerSpecs.TryGetValue(fastenerId ?? string.Empty, out FastenerSpec spec) ? spec : null;

        /// <summary>Builds another blueprint here. Only while the station is empty.</summary>
        public bool TrySetBlueprint(BlueprintDefinition definition)
        {
            if (definition == null || !IsEmpty)
            {
                return false;
            }

            if (definition != active)
            {
                Setup(definition);
                BlueprintChanged?.Invoke();
                Changed?.Invoke();
            }

            return true;
        }

        public BlueprintDefinition FindBlueprint(string blueprintId)
        {
            if (active != null && active.Id == blueprintId)
            {
                return active;
            }

            foreach (BlueprintDefinition candidate in knownBlueprints)
            {
                if (candidate != null && candidate.Id == blueprintId)
                {
                    return candidate;
                }
            }

            return blueprint != null && blueprint.Id == blueprintId ? blueprint : null;
        }

        /// <summary>
        /// Removes the finished (or started) structure: its boards are taken away (delivered to the customer) and the
        /// station starts over with the same blueprint.
        /// </summary>
        public void ClearAll()
        {
            if (installed != null)
            {
                foreach (Carryable part in installed)
                {
                    if (part != null)
                    {
                        Destroy(part.gameObject);
                    }
                }
            }

            Setup(active);
            Changed?.Invoke();
        }

        private void Setup(BlueprintDefinition definition)
        {
            foreach (GameObject visual in visuals)
            {
                Destroy(visual);
            }

            visuals.Clear();
            foreach (Mesh mesh in ghostMeshes)
            {
                Destroy(mesh);
            }

            ghostMeshes.Clear();
            drivingHead = null;

            active = definition;
            Build = new AssemblyBuild(definition.ToDomain());
            int count = definition.Slots.Count;
            installed = new Carryable[count];
            parentsBeforeInstall = new Transform[count];
            ghosts = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                ghosts[i] = CreateGhost(definition.Slots[i]);
                visuals.Add(ghosts[i]);
            }

            int connections = definition.Connections.Count;
            pointMarkers = new GameObject[connections][];
            holeMarkers = new GameObject[connections][];
            screwHeads = new GameObject[connections][];
            for (int c = 0; c < connections; c++)
            {
                int points = definition.Connections[c].points.Length;
                pointMarkers[c] = new GameObject[points];
                holeMarkers[c] = new GameObject[points];
                screwHeads[c] = new GameObject[points];
                for (int p = 0; p < points; p++)
                {
                    pointMarkers[c][p] = CreateFlatBox($"Point {definition.Connections[c].id}.{p}", c, p,
                        MarkerSize, 0.0005f, 0f, pointMarkerMaterial);
                }
            }

            // A structure loaded complete from a save must not announce itself again.
            Build.Completed += () =>
            {
                if (!restoring)
                {
                    Completed?.Invoke();
                }
            };
            UpdatePointMarkers();
        }

        /// <summary>Save state: which board (by <see cref="PersistentId"/>) is in which slot; holes and screws per point.</summary>
        public AssemblyData CaptureState(string stationId)
        {
            var parts = new List<InstalledPartData>();
            for (int i = 0; i < installed.Length; i++)
            {
                if (installed[i] != null && installed[i].TryGetComponent(out PersistentId boardId))
                {
                    parts.Add(new InstalledPartData { slotId = active.Slots[i].id, boardId = boardId.Id });
                }
            }

            var connections = new List<FastenedConnectionData>();
            foreach (BlueprintDefinition.Connection connection in active.Connections)
            {
                var points = new List<int>();
                var heights = new List<long>();
                var pilots = new List<int>();
                for (int p = 0; p < connection.points.Length; p++)
                {
                    Length? head = Build.HeadHeight(connection.id, p);
                    if (head.HasValue)
                    {
                        points.Add(p);
                        heights.Add(head.Value.TenthsOfMillimeter);
                    }

                    if (Build.HasPilotHole(connection.id, p))
                    {
                        pilots.Add(p);
                    }
                }

                if (points.Count > 0 || pilots.Count > 0)
                {
                    connections.Add(new FastenedConnectionData
                    {
                        connectionId = connection.id,
                        points = points.ToArray(),
                        headHeightsTenths = heights.ToArray(),
                        pilotHoles = pilots.ToArray()
                    });
                }
            }

            return new AssemblyData
            {
                stationId = stationId,
                blueprintId = Build.Blueprint.Id,
                parts = parts.ToArray(),
                fastened = connections.ToArray()
            };
        }

        /// <summary>
        /// Rebuilds a saved state on this (empty) station: switches to the saved blueprint, installs the boards, then
        /// pilot holes and screws, with the same rules as in play. Whatever no longer fits (e.g. the blueprint changed)
        /// is skipped with a warning and stays loose.
        /// </summary>
        public void RestoreState(AssemblyData data, Func<string, Carryable> findBoard)
        {
            BlueprintDefinition saved = FindBlueprint(data.blueprintId);
            if (saved == null || !TrySetBlueprint(saved))
            {
                Debug.LogWarning($"[{nameof(AssemblyStation)}] '{name}': saved blueprint '{data.blueprintId}' is unknown " +
                                 "here; the saved assembly is not restored.", this);
                return;
            }

            restoring = true;
            try
            {
                RestoreParts(data.parts ?? Array.Empty<InstalledPartData>(), findBoard);
                RestoreFasteners(data.fastened ?? Array.Empty<FastenedConnectionData>());
            }
            finally
            {
                restoring = false;
            }
        }

        private void RestoreParts(InstalledPartData[] parts, Func<string, Carryable> findBoard)
        {
            // Prerequisites must be installed first; the saved order doesn't matter, so repeat until nothing changes.
            var pending = new List<InstalledPartData>(parts);
            bool progress = true;
            while (pending.Count > 0 && progress)
            {
                progress = false;
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    int index = SlotIndex(pending[i].slotId);
                    Carryable board = findBoard(pending[i].boardId);
                    if (index >= 0 && board != null && TryInstallAt(index, board))
                    {
                        pending.RemoveAt(i);
                        progress = true;
                    }
                }
            }

            foreach (InstalledPartData part in pending)
            {
                Debug.LogWarning($"[{nameof(AssemblyStation)}] '{name}': board '{part.boardId}' could not be put back " +
                                 $"into slot '{part.slotId}'; it stays loose.", this);
            }
        }

        private void RestoreFasteners(FastenedConnectionData[] connections)
        {
            foreach (FastenedConnectionData connection in connections)
            {
                int c = ConnectionIndex(connection.connectionId);
                if (c < 0)
                {
                    Debug.LogWarning($"[{nameof(AssemblyStation)}] '{name}': connection '{connection.connectionId}' unknown; skipped.", this);
                    continue;
                }

                foreach (int point in connection.pilotHoles ?? Array.Empty<int>())
                {
                    if (Build.RestorePilot(connection.connectionId, point) == PilotResult.Drilled)
                    {
                        AddHole(c, point);
                    }
                }

                FastenerSpec spec = Fastener(active.Connections[c].fastenerId);
                int[] points = connection.points ?? Array.Empty<int>();
                long[] heights = connection.headHeightsTenths ?? Array.Empty<long>();
                for (int i = 0; i < points.Length; i++)
                {
                    Length head = i < heights.Length ? Length.FromTenthsOfMillimeter(heights[i]) : Length.Zero;
                    FastenResult result = Build.RestoreFastened(connection.connectionId, points[i], head, spec);
                    if (result == FastenResult.Fastened)
                    {
                        ShowScrew(c, points[i]);
                    }
                    else
                    {
                        Debug.LogWarning($"[{nameof(AssemblyStation)}] '{name}': fastener {connection.connectionId}.{points[i]} " +
                                         $"not restored: {result}", this);
                    }
                }
            }

            UpdatePointMarkers();
            Changed?.Invoke();
        }

        private int SlotIndex(string slotId)
        {
            for (int i = 0; i < active.Slots.Count; i++)
            {
                if (active.Slots[i].id == slotId)
                {
                    return i;
                }
            }

            return -1;
        }

        private int ConnectionIndex(string connectionId)
        {
            for (int c = 0; c < active.Connections.Count; c++)
            {
                if (active.Connections[c].id == connectionId)
                {
                    return c;
                }
            }

            return -1;
        }

        public string ConnectionId(int connection) => active.Connections[connection].id;

        public string FastenerIdAt(int connection) => active.Connections[connection].fastenerId;

        public Vector3 PointPosition(int connection, int point) =>
            transform.TransformPoint(active.Connections[connection].points[point].localPosition);

        public Vector3 PointDirection(int connection, int point) =>
            transform.TransformDirection(active.Connections[connection].points[point].localDirection).normalized;

        /// <summary>
        /// The fastening point nearest to <paramref name="worldPoint"/> (within <paramref name="maxDistance"/> m) that
        /// matches the query.
        /// </summary>
        public bool TryFindPoint(Vector3 worldPoint, float maxDistance, PointQuery query, out int connection, out int point)
        {
            connection = -1;
            point = -1;
            float best = maxDistance;
            for (int c = 0; c < active.Connections.Count; c++)
            {
                for (int p = 0; p < active.Connections[c].points.Length; p++)
                {
                    if (!Matches(c, p, query))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(worldPoint, PointPosition(c, p));
                    if (distance <= best)
                    {
                        best = distance;
                        connection = c;
                        point = p;
                    }
                }
            }

            return connection >= 0;
        }

        /// <summary>Any point at all that matches the query (for hints).</summary>
        public bool HasPoint(PointQuery query)
        {
            for (int c = 0; c < active.Connections.Count; c++)
            {
                for (int p = 0; p < active.Connections[c].points.Length; p++)
                {
                    if (Matches(c, p, query))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool Matches(int connection, int point, PointQuery query)
        {
            if (!PartsInstalled(connection))
            {
                return false;
            }

            string id = active.Connections[connection].id;
            Length? head = Build.HeadHeight(id, point);
            return query == PointQuery.ForPilotHole
                ? !head.HasValue && !Build.HasPilotHole(id, point)
                : !head.HasValue || FasteningRules.Seat(head.Value) == ScrewSeat.Proud;
        }

        private bool PartsInstalled(int connection)
        {
            BlueprintDefinition.Connection spec = active.Connections[connection];
            return Build.GetPiece(spec.partA) != null && Build.GetPiece(spec.partB) != null;
        }

        /// <summary>
        /// True if a screw here would split the board: the point needs a pilot hole (near a board end) and has none yet.
        /// </summary>
        public bool NeedsPilotHere(int connection, int point)
        {
            string id = active.Connections[connection].id;
            FastenerSpec spec = Fastener(FastenerIdAt(connection));
            return spec != null && !Build.IsFastened(id, point) && !Build.HasPilotHole(id, point) &&
                   Build.NeedsPilotHole(id, point, spec);
        }

        public PilotResult DrillPilot(int connection, int point)
        {
            PilotResult result = Build.TryDrillPilot(active.Connections[connection].id, point);
            if (result == PilotResult.Drilled)
            {
                AddHole(connection, point);
                UpdatePointMarkers();
                Changed?.Invoke();
            }

            return result;
        }

        /// <summary>Drives a new screw (head ending at <paramref name="headHeight"/>) and shows it.</summary>
        public FastenResult Fasten(int connection, int point, FastenerSpec fastener, Length headHeight)
        {
            FastenResult result = Build.TryFasten(active.Connections[connection].id, point, fastener, headHeight);
            if (result == FastenResult.Fastened)
            {
                HideDrivingHead();
                ShowScrew(connection, point);
                UpdatePointMarkers();
                Changed?.Invoke();
            }

            return result;
        }

        /// <summary>Drives a proud screw further.</summary>
        public FastenResult DriveFurther(int connection, int point, Length headHeight)
        {
            FastenResult result = Build.TryDriveFurther(active.Connections[connection].id, point, headHeight);
            if (result == FastenResult.Fastened)
            {
                HideDrivingHead();
                ShowScrew(connection, point);
                UpdatePointMarkers();
                Changed?.Invoke();
            }

            return result;
        }

        /// <summary>A screw being driven right now: its head at the given height above the surface.</summary>
        public void ShowDrivingHead(int connection, int point, Length headHeight)
        {
            if (drivingHead == null)
            {
                drivingHead = CreateFlatBox("Driving screw", connection, point, ScrewHeadSize, ScrewHeadHeight, 0f,
                    screwHeadMaterial);
            }

            PlaceOnSurface(drivingHead.transform, connection, point, ScrewHeadHeight, (float)headHeight.Meters);
            drivingHead.SetActive(true);
            if (screwHeads[connection][point] != null)
            {
                screwHeads[connection][point].SetActive(false);
            }
        }

        public void HideDrivingHead()
        {
            if (drivingHead != null)
            {
                visuals.Remove(drivingHead);
                Destroy(drivingHead);
                drivingHead = null;
            }

            for (int c = 0; c < screwHeads.Length; c++)
            {
                for (int p = 0; p < screwHeads[c].Length; p++)
                {
                    if (screwHeads[c][p] != null)
                    {
                        screwHeads[c][p].SetActive(true);
                    }
                }
            }
        }

        /// <summary>The quality report as text (every component on its own line).</summary>
        public string ReportText(string fastenerName)
        {
            double? sawdust = waste != null ? waste.Log.TotalVolumeCubicMeters * 1e6 : (double?)null;
            return AssemblyReportText.Format(Build.Blueprint, Build.CreateReport(), fastenerName, sawdust);
        }

        private void ShowScrew(int connection, int point)
        {
            string id = active.Connections[connection].id;
            Length head = Build.HeadHeight(id, point) ?? Length.Zero;
            GameObject screw = screwHeads[connection][point];
            if (screw == null)
            {
                screw = CreateFlatBox($"Screw {id}.{point}", connection, point, ScrewHeadSize, ScrewHeadHeight, 0f,
                    screwHeadMaterial);
                screwHeads[connection][point] = screw;
            }

            // The head stands out by its height (an overdriven one sits in a crushed hole, just below the surface).
            PlaceOnSurface(screw.transform, connection, point, ScrewHeadHeight, (float)head.Meters);

            if (Build.IsSplit(id, point))
            {
                AddSplit(connection, point);
            }
        }

        private void AddHole(int connection, int point)
        {
            if (holeMarkers[connection][point] == null)
            {
                holeMarkers[connection][point] = CreateFlatBox($"Pilot hole {active.Connections[connection].id}.{point}",
                    connection, point, HoleSize, 0.0006f, 0f, holeMaterial);
            }
        }

        /// <summary>A dark crack along the board the screw went through (part A), starting at the screw.</summary>
        private void AddSplit(int connection, int point)
        {
            BlueprintDefinition.Connection spec = active.Connections[connection];
            int slot = SlotIndex(spec.partA);
            if (slot < 0)
            {
                return;
            }

            Vector3 along = SlotRotation(slot) * Vector3.forward;
            Vector3 position = PointPosition(connection, point);
            Vector3 toEnd = Vector3.Dot(SlotPosition(slot) - position, along) > 0f ? -along : along;
            Vector3 direction = PointDirection(connection, point);

            var crack = new GameObject($"Split {spec.id}.{point}");
            crack.transform.SetParent(transform, false);
            crack.transform.SetPositionAndRotation(position + toEnd * (SplitLength / 2f) - direction * 0.0004f,
                Quaternion.LookRotation(toEnd, -direction));
            crack.transform.localScale = new Vector3(0.0012f, 0.0008f, SplitLength);
            AddRenderer(crack, holeMaterial);
            visuals.Add(crack);
        }

        private void UpdatePointMarkers()
        {
            for (int c = 0; c < pointMarkers.Length; c++)
            {
                for (int p = 0; p < pointMarkers[c].Length; p++)
                {
                    bool open = PartsInstalled(c) && !Build.IsFastened(active.Connections[c].id, p);
                    pointMarkers[c][p].SetActive(open);
                    if (open && pilotPointMaterial != null)
                    {
                        // Red = drill a pilot hole first; orange = ready for a screw.
                        pointMarkers[c][p].GetComponent<MeshRenderer>().sharedMaterial =
                            NeedsPilotHere(c, p) ? pilotPointMaterial : pointMarkerMaterial;
                    }
                }
            }
        }

        /// <summary>A small box on the surface at a fastening point, facing outwards (against the driving direction).</summary>
        private GameObject CreateFlatBox(string objectName, int connection, int point, float size, float height,
            float heightAboveSurface, Material material)
        {
            var box = new GameObject(objectName);
            box.transform.SetParent(transform, false);
            PlaceOnSurface(box.transform, connection, point, height, heightAboveSurface);
            box.transform.localScale = new Vector3(size, size, height);
            AddRenderer(box, material);
            visuals.Add(box);
            return box;
        }

        /// <summary>
        /// Puts a box of the given height on the surface at a fastening point, raised by <paramref name="aboveSurface"/> m
        /// (a proud screw head). Sunk heads are drawn level with the surface, otherwise the board would hide them.
        /// </summary>
        private void PlaceOnSurface(Transform box, int connection, int point, float height, float aboveSurface)
        {
            Vector3 direction = PointDirection(connection, point);
            float raise = Mathf.Max(0f, aboveSurface);
            box.SetPositionAndRotation(PointPosition(connection, point) - direction * (raise + height / 2f),
                Quaternion.LookRotation(direction));
        }

        private static void AddRenderer(GameObject target, Material material)
        {
            target.AddComponent<MeshFilter>().sharedMesh = unitCube;
            var meshRenderer = target.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in ghostMeshes)
            {
                Destroy(mesh);
            }
        }

        public Vector3 SlotPosition(int index) => transform.TransformPoint(active.Slots[index].localPosition);

        public Quaternion SlotRotation(int index) => transform.rotation * Quaternion.Euler(active.Slots[index].localEulerAngles);

        /// <summary>The board installed in a slot, or null.</summary>
        public Carryable InstalledAt(int index) => installed[index];

        public bool TryGetSnap(Carryable carried, Vector3 aimPoint, out SnapPlacement snap)
        {
            snap = default;
            if (!enabled || !carried.TryGetComponent(out TimberBoard board) || board.Piece == null)
            {
                return false;
            }

            // Among the empty slots near the aim point, prefer the nearest one the piece can go into; otherwise
            // explain the nearest one.
            int nearest = -1;
            int nearestPlaceable = -1;
            float nearestDistance = float.MaxValue;
            float nearestPlaceableDistance = float.MaxValue;
            for (int i = 0; i < installed.Length; i++)
            {
                if (installed[i] != null)
                {
                    continue;
                }

                float distance = DistanceToSlot(i, aimPoint);
                if (distance > snapDistance)
                {
                    continue;
                }

                if (distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }

                if (distance < nearestPlaceableDistance &&
                    Build.CanPlace(active.Slots[i].id, board.Piece) == PlaceResult.Placed)
                {
                    nearestPlaceable = i;
                    nearestPlaceableDistance = distance;
                }
            }

            int chosen = nearestPlaceable >= 0 ? nearestPlaceable : nearest;
            if (chosen < 0)
            {
                return false;
            }

            PlaceResult result = Build.CanPlace(active.Slots[chosen].id, board.Piece);
            snap = new SnapPlacement(SlotPosition(chosen), SlotRotation(chosen), result == PlaceResult.Placed,
                Describe(chosen, board.Piece, result), chosen);
            return true;
        }

        public void Install(Carryable carryable, SnapPlacement snap)
        {
            if (!TryInstallAt(snap.SlotIndex, carryable))
            {
                // The snap was checked a moment ago; if the state changed, the board just stays where it was put.
                Debug.LogWarning($"[{nameof(AssemblyStation)}] Could not install '{carryable.name}' into slot {snap.SlotIndex}.", this);
            }
        }

        private bool TryInstallAt(int index, Carryable carryable)
        {
            if (installed[index] != null || !carryable.TryGetComponent(out TimberBoard board) || board.Piece == null ||
                Build.TryPlace(active.Slots[index].id, board.Piece) != PlaceResult.Placed)
            {
                return false;
            }

            parentsBeforeInstall[index] = carryable.transform.parent;
            carryable.transform.SetParent(transform, true);
            carryable.transform.SetPositionAndRotation(SlotPosition(index), SlotRotation(index));
            carryable.SetInstalled(this);
            installed[index] = carryable;
            ghosts[index].SetActive(false);
            UpdatePointMarkers();
            Changed?.Invoke();
            return true;
        }

        public bool CanRelease(Carryable carryable, out string reason)
        {
            int index = Array.IndexOf(installed, carryable);
            RemoveResult result = index < 0 ? RemoveResult.SlotEmpty : Build.CanRemove(active.Slots[index].id);
            reason = result == RemoveResult.Fastened
                ? Loc.T($"Screwed into the {DisplayName}", $"{DisplayName} içine vidalı")
                : null;
            return result == RemoveResult.Removed;
        }

        public void Release(Carryable carryable)
        {
            int index = Array.IndexOf(installed, carryable);
            if (index < 0 || Build.TryRemove(active.Slots[index].id, out _) != RemoveResult.Removed)
            {
                return;
            }

            carryable.transform.SetParent(parentsBeforeInstall[index], true);
            carryable.SetInstalled(null);
            installed[index] = null;
            ghosts[index].SetActive(true);
            UpdatePointMarkers();
            Changed?.Invoke();
        }

        private string Describe(int index, TimberPiece piece, PlaceResult result)
        {
            BlueprintDefinition.Slot slot = active.Slots[index];
            Build.Blueprint.TryGetSlot(slot.id, out PartSlot partSlot);
            string name = partSlot.DisplayName;
            switch (result)
            {
                case PlaceResult.Placed:
                    SlotFit fit = Build.CheckFit(partSlot, piece);
                    return fit.IsAccurate
                        ? Loc.T($"{name}: fits", $"{name}: uyuyor")
                        : Loc.T($"{name}: fits, but {DeviationText(fit)} (inaccurate)", $"{name}: uyuyor ama {DeviationText(fit)} (ölçü dışı)");
                case PlaceResult.DoesNotFit:
                    return Loc.T($"Doesn't fit {name}: needs {partSlot.Dimensions}, this one {piece.Dimensions}",
                        $"{name} yerine uymuyor: {partSlot.Dimensions} gerekli, bu {piece.Dimensions}");
                case PlaceResult.PrerequisiteMissing:
                    var first = new List<string>();
                    foreach (string prerequisite in slot.prerequisites)
                    {
                        first.Add(Build.Blueprint.TryGetSlot(prerequisite, out PartSlot other) ? other.DisplayName : prerequisite);
                    }

                    return Loc.T($"{name}: place {string.Join(", ", first)} first",
                        $"{name}: önce {string.Join(", ", first)} takılmalı");
                default:
                    return $"{name}: {result}";
            }
        }

        /// <summary>The largest deviation as text, e.g. "2.5 mm too long".</summary>
        private static string DeviationText(SlotFit fit)
        {
            ToleranceCheck worst = fit.Length;
            int what = 0;
            if (fit.Width.Deviation.Abs() > worst.Deviation.Abs())
            {
                worst = fit.Width;
                what = 1;
            }

            if (fit.Thickness.Deviation.Abs() > worst.Deviation.Abs())
            {
                worst = fit.Thickness;
                what = 2;
            }

            bool tooMuch = worst.Deviation > Length.Zero;
            string adjective = what == 0 ? (tooMuch ? Loc.T("too long", "fazla uzun") : Loc.T("too short", "fazla kısa"))
                : what == 1 ? (tooMuch ? Loc.T("too wide", "fazla geniş") : Loc.T("too narrow", "fazla dar"))
                : (tooMuch ? Loc.T("too thick", "fazla kalın") : Loc.T("too thin", "fazla ince"));
            return $"{worst.Deviation.Abs()} {adjective}";
        }

        /// <summary>Distance from a point to the slot's nominal box (0 inside).</summary>
        private float DistanceToSlot(int index, Vector3 point)
        {
            BlueprintDefinition.Slot slot = active.Slots[index];
            Vector3 half = SlotSize(slot) * 0.5f;
            Vector3 local = Quaternion.Inverse(SlotRotation(index)) * (point - SlotPosition(index));
            Vector3 outside = new Vector3(
                Mathf.Max(0f, Mathf.Abs(local.x) - half.x),
                Mathf.Max(0f, Mathf.Abs(local.y) - half.y),
                Mathf.Max(0f, Mathf.Abs(local.z) - half.z));
            return outside.magnitude;
        }

        private static Vector3 SlotSize(BlueprintDefinition.Slot slot) => new Vector3(
            slot.widthMillimeters / 1000f, slot.thicknessMillimeters / 1000f, slot.lengthMillimeters / 1000f);

        private GameObject CreateGhost(BlueprintDefinition.Slot slot)
        {
            // No collider: the ghost must not block placement or be aimed at.
            var ghost = new GameObject($"Ghost {slot.id}");
            ghost.transform.SetParent(transform, false);
            ghost.transform.localPosition = slot.localPosition;
            ghost.transform.localRotation = Quaternion.Euler(slot.localEulerAngles);

            Mesh mesh = BoxMeshBuilder.Create(SlotSize(slot), $"Ghost {slot.id}");
            ghostMeshes.Add(mesh);
            ghost.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = ghost.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = ghostMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return ghost;
        }
    }
}
