using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Construction
{
    /// <summary>
    /// A place where one structure is built from a <see cref="BlueprintDefinition"/> (e.g. the assembly table).
    /// Shows translucent ghosts of the empty slots; a carried board aimed near a slot snaps into it when it fits
    /// (through <see cref="IPlacementTarget"/>, so the carrier knows nothing about assemblies). The rules are the
    /// domain <see cref="AssemblyBuild"/>. This transform is the assembly origin (slot poses are relative to it).
    /// </summary>
    public sealed class AssemblyStation : MonoBehaviour, IPlacementTarget
    {
        [SerializeField] private BlueprintDefinition blueprint;

        [Tooltip("Translucent material for the ghosts of empty slots.")]
        [SerializeField] private Material ghostMaterial;

        [Tooltip("A carried board snaps when the aim point is within this distance (m) of an empty slot.")]
        [SerializeField, Min(0.01f)] private float snapDistance = 0.08f;

        [Header("Fastening points")]
        [Tooltip("Marker of a fastening point that can be screwed now (both parts installed).")]
        [SerializeField] private Material pointMarkerMaterial;

        [Tooltip("Material of the visible screw heads.")]
        [SerializeField] private Material screwHeadMaterial;

        [Tooltip("Optional: the workshop's sawdust total, shown in the quality report.")]
        [SerializeField] private Cutting.WorkshopWaste waste;

        // Visual sizes (meters): an 8 mm marker square and a 9 mm screw head standing 1 mm proud of the surface.
        private const float MarkerSize = 0.008f;
        private const float ScrewHeadSize = 0.009f;
        private const float ScrewHeadHeight = 0.0015f;

        private static Mesh unitCube;

        private readonly List<Mesh> ghostMeshes = new List<Mesh>();
        private GameObject[] ghosts;
        private Carryable[] installed;
        private Transform[] parentsBeforeInstall;
        private GameObject[][] pointMarkers;

        public AssemblyBuild Build { get; private set; }

        public BlueprintDefinition Definition => blueprint;

        public string DisplayName => Build != null ? Build.Blueprint.DisplayName : name;

        /// <summary>Raised after a part was installed or taken out, or a fastener was driven.</summary>
        public event Action Changed;

        /// <summary>Raised once when the last part / fastener completes the structure.</summary>
        public event Action Completed;

        private void Awake()
        {
            if (blueprint == null)
            {
                Debug.LogError($"{nameof(AssemblyStation)} '{name}': blueprint is not assigned.", this);
                enabled = false;
                return;
            }

            Build = new AssemblyBuild(blueprint.ToDomain());
            int count = blueprint.Slots.Count;
            installed = new Carryable[count];
            parentsBeforeInstall = new Transform[count];
            ghosts = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                ghosts[i] = CreateGhost(blueprint.Slots[i]);
            }

            unitCube ??= BoxMeshBuilder.Create(Vector3.one, "Unit cube");
            pointMarkers = new GameObject[blueprint.Connections.Count][];
            for (int c = 0; c < pointMarkers.Length; c++)
            {
                BlueprintDefinition.FasteningPoint[] points = blueprint.Connections[c].points;
                pointMarkers[c] = new GameObject[points.Length];
                for (int p = 0; p < points.Length; p++)
                {
                    pointMarkers[c][p] = CreateFlatBox($"Point {blueprint.Connections[c].id}.{p}", c, p,
                        MarkerSize, 0.0005f, pointMarkerMaterial);
                }
            }

            Build.Completed += () => Completed?.Invoke();
            UpdatePointMarkers();
        }

        public Vector3 PointPosition(int connection, int point) =>
            transform.TransformPoint(blueprint.Connections[connection].points[point].localPosition);

        public Vector3 PointDirection(int connection, int point) =>
            transform.TransformDirection(blueprint.Connections[connection].points[point].localDirection).normalized;

        /// <summary>
        /// The fastening point nearest to <paramref name="worldPoint"/> (within <paramref name="maxDistance"/> m) that
        /// can be fastened now: both parts installed and no fastener in it yet.
        /// </summary>
        public bool TryFindOpenPoint(Vector3 worldPoint, float maxDistance, out int connection, out int point)
        {
            connection = -1;
            point = -1;
            float best = maxDistance;
            for (int c = 0; c < blueprint.Connections.Count; c++)
            {
                for (int p = 0; p < blueprint.Connections[c].points.Length; p++)
                {
                    if (!IsOpen(c, p))
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

        /// <summary>Drives a fastener into a point (domain rules) and shows its head.</summary>
        public FastenResult Fasten(int connection, int point, FastenerSpec fastener)
        {
            FastenResult result = Build.TryFasten(blueprint.Connections[connection].id, point, fastener);
            if (result == FastenResult.Fastened)
            {
                CreateFlatBox($"Screw {blueprint.Connections[connection].id}.{point}", connection, point,
                    ScrewHeadSize, ScrewHeadHeight, screwHeadMaterial);
                UpdatePointMarkers();
                Changed?.Invoke();
            }

            return result;
        }

        /// <summary>The quality report as text (every component on its own line).</summary>
        public string ReportText(string fastenerName)
        {
            double? sawdust = waste != null ? waste.Log.TotalVolumeCubicMeters * 1e6 : (double?)null;
            return AssemblyReportText.Format(Build.Blueprint, Build.CreateReport(), fastenerName, sawdust);
        }

        private bool IsOpen(int connection, int point)
        {
            BlueprintDefinition.Connection spec = blueprint.Connections[connection];
            return Build.GetPiece(spec.partA) != null && Build.GetPiece(spec.partB) != null &&
                   !Build.IsFastened(spec.id, point);
        }

        private void UpdatePointMarkers()
        {
            for (int c = 0; c < pointMarkers.Length; c++)
            {
                for (int p = 0; p < pointMarkers[c].Length; p++)
                {
                    pointMarkers[c][p].SetActive(IsOpen(c, p));
                }
            }
        }

        /// <summary>A small square on the surface at a fastening point, facing outwards (against the driving direction).</summary>
        private GameObject CreateFlatBox(string objectName, int connection, int point, float size, float height,
            Material material)
        {
            var box = new GameObject(objectName);
            box.transform.SetParent(transform, false);
            Vector3 direction = PointDirection(connection, point);
            box.transform.SetPositionAndRotation(PointPosition(connection, point) - direction * (height / 2f),
                Quaternion.LookRotation(direction));
            box.transform.localScale = new Vector3(size, size, height);
            box.AddComponent<MeshFilter>().sharedMesh = unitCube;
            var meshRenderer = box.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return box;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in ghostMeshes)
            {
                Destroy(mesh);
            }
        }

        public Vector3 SlotPosition(int index) => transform.TransformPoint(blueprint.Slots[index].localPosition);

        public Quaternion SlotRotation(int index) => transform.rotation * Quaternion.Euler(blueprint.Slots[index].localEulerAngles);

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
                    Build.CanPlace(blueprint.Slots[i].id, board.Piece) == PlaceResult.Placed)
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

            PlaceResult result = Build.CanPlace(blueprint.Slots[chosen].id, board.Piece);
            snap = new SnapPlacement(SlotPosition(chosen), SlotRotation(chosen), result == PlaceResult.Placed,
                Describe(chosen, board.Piece, result), chosen);
            return true;
        }

        public void Install(Carryable carryable, SnapPlacement snap)
        {
            int index = snap.SlotIndex;
            if (!carryable.TryGetComponent(out TimberBoard board) ||
                Build.TryPlace(blueprint.Slots[index].id, board.Piece) != PlaceResult.Placed)
            {
                // The snap was checked a moment ago; if the state changed, the board just stays where it was put.
                Debug.LogWarning($"[{nameof(AssemblyStation)}] Could not install '{carryable.name}' into slot {index}.", this);
                return;
            }

            parentsBeforeInstall[index] = carryable.transform.parent;
            carryable.transform.SetParent(transform, true);
            carryable.transform.SetPositionAndRotation(SlotPosition(index), SlotRotation(index));
            carryable.SetInstalled(this);
            installed[index] = carryable;
            ghosts[index].SetActive(false);
            UpdatePointMarkers();
            Changed?.Invoke();
        }

        public bool CanRelease(Carryable carryable, out string reason)
        {
            int index = Array.IndexOf(installed, carryable);
            RemoveResult result = index < 0 ? RemoveResult.SlotEmpty : Build.CanRemove(blueprint.Slots[index].id);
            reason = result == RemoveResult.Fastened ? $"Screwed into the {DisplayName}" : null;
            return result == RemoveResult.Removed;
        }

        public void Release(Carryable carryable)
        {
            int index = Array.IndexOf(installed, carryable);
            if (index < 0 || Build.TryRemove(blueprint.Slots[index].id, out _) != RemoveResult.Removed)
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
            BlueprintDefinition.Slot slot = blueprint.Slots[index];
            Build.Blueprint.TryGetSlot(slot.id, out PartSlot partSlot);
            switch (result)
            {
                case PlaceResult.Placed:
                    SlotFit fit = Build.CheckFit(partSlot, piece);
                    return fit.IsAccurate
                        ? $"{slot.displayName}: fits"
                        : $"{slot.displayName}: fits, but {DeviationText(fit)} (inaccurate)";
                case PlaceResult.DoesNotFit:
                    return $"Doesn't fit {slot.displayName}: needs {partSlot.Dimensions}, this one {piece.Dimensions}";
                case PlaceResult.PrerequisiteMissing:
                    return $"{slot.displayName}: place {string.Join(", ", slot.prerequisites)} first";
                default:
                    return $"{slot.displayName}: {result}";
            }
        }

        /// <summary>The largest deviation as text, e.g. "2.5 mm too long".</summary>
        private static string DeviationText(SlotFit fit)
        {
            ToleranceCheck worst = fit.Length;
            string what = "long";
            if (fit.Width.Deviation.Abs() > worst.Deviation.Abs())
            {
                worst = fit.Width;
                what = "wide";
            }

            if (fit.Thickness.Deviation.Abs() > worst.Deviation.Abs())
            {
                worst = fit.Thickness;
                what = "thick";
            }

            bool tooMuch = worst.Deviation > Length.Zero;
            string adjective = what == "long" ? (tooMuch ? "too long" : "too short")
                : what == "wide" ? (tooMuch ? "too wide" : "too narrow")
                : (tooMuch ? "too thick" : "too thin");
            return $"{worst.Deviation.Abs()} {adjective}";
        }

        /// <summary>Distance from a point to the slot's nominal box (0 inside).</summary>
        private float DistanceToSlot(int index, Vector3 point)
        {
            BlueprintDefinition.Slot slot = blueprint.Slots[index];
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
