using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Inventory;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// Cordless drill-driver in hand: aim at an orange fastening point of an assembly and hold Use (LMB) to drive a
    /// screw. The rules (both parts installed, right fastener, each point once) are the domain <see cref="AssemblyBuild"/>;
    /// screws are unlimited until the material economy (Phase 7, D-032).
    /// </summary>
    public sealed class DrillDriverTool : MonoBehaviour, IHeldTool
    {
        [Tooltip("The fastener this driver drives (one type in the prototype).")]
        [SerializeField] private FastenerDefinition fastener;

        [SerializeField, Min(0.1f)] private float range = 2f;

        [Tooltip("The driver snaps to a fastening point within this distance (m) of the aimed point.")]
        [SerializeField, Min(0.005f)] private float pointSnapDistance = 0.03f;

        [Tooltip("Seconds to drive one screw.")]
        [SerializeField, Min(0.1f)] private float driveSeconds = 0.8f;

        [SerializeField] private LayerMask mask = Physics.DefaultRaycastLayers;

        [Header("Push animation (camera space)")]
        [SerializeField, Min(0f)] private float pushLength = 0.02f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private InputAction useAction;
        private string useKeyLabel;
        private FastenerSpec fastenerSpec;
        private PickupItem pickup;
        private HeldToolContext context;
        private int ignoreInputFrame = -1;

        private AssemblyStation targetStation;
        private int targetConnection = -1;
        private int targetPoint = -1;
        private float progress;
        private int shownPercent = -1;

        private AssemblyStation observedStation;
        private AssemblyQualityReport observedReport;
        private string observedProgressText;
        private string observedCompleteText;
        private string observedAimText;
        private string observedHoldText;

        private string statusLine;
        private string resultLine;

        public string Readout { get; private set; }

        public event Action ReadoutChanged;

        private void Awake()
        {
            pickup = GetComponent<PickupItem>();
            if (fastener == null)
            {
                Debug.LogError($"{nameof(DrillDriverTool)} '{name}': fastener definition is not assigned.", this);
                enabled = false;
                return;
            }

            fastenerSpec = fastener.ToDomain();
            useAction = InputSystem.actions.FindAction("Player/UseTool", throwIfNotFound: true);
            useKeyLabel = useAction.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        public void Equip(HeldToolContext toolContext)
        {
            context = toolContext;
            // The click that resumes from the pause menu must not start driving.
            ignoreInputFrame = Time.frameCount;
        }

        public void Unequip()
        {
            context = null;
            ClearTarget();
            resultLine = null;
            SetLines(null, null);
        }

        private void Update()
        {
            // Also runs while lying on the table (active GameObject); only work when held.
            if (context == null)
            {
                return;
            }

            Transform eyes = context.Eyes;
            var ray = new Ray(eyes.position, eyes.forward);
            AssemblyStation station = null;
            if (PhysicsQueries.RaycastIgnoring(ray, range, mask, context.PlayerRoot, hitBuffer, out RaycastHit hit))
            {
                station = hit.collider.GetComponentInParent<AssemblyStation>();
            }

            if (station == null)
            {
                ClearTarget();
                SetLines(null, resultLine);
                return;
            }

            ObserveStation(station);
            string progressText = observedProgressText;

            if (observedReport.IsComplete)
            {
                ClearTarget();
                SetLines(observedCompleteText, resultLine);
                return;
            }

            if (!station.TryFindOpenPoint(hit.point, pointSnapDistance, out int connection, out int point))
            {
                ClearTarget();
                SetLines(observedAimText, resultLine);
                return;
            }

            if (station != targetStation || connection != targetConnection || point != targetPoint)
            {
                ClearTarget();
                targetStation = station;
                targetConnection = connection;
                targetPoint = point;
            }

            bool driving = useAction.IsPressed() && Time.frameCount != ignoreInputFrame;
            if (!driving)
            {
                pickup.HandOffset = Vector3.zero;
                shownPercent = -1;
                SetLines(observedHoldText, resultLine);
                return;
            }

            progress += Time.deltaTime / driveSeconds;
            pickup.HandOffset = Vector3.forward * (pushLength * Mathf.Clamp01(progress));
            if (progress < 1f)
            {
                int percent = Mathf.FloorToInt(progress * 100f);
                if (percent != shownPercent)
                {
                    shownPercent = percent;
                    SetLines($"{progressText}\nDriving  {percent}%", resultLine);
                }

                return;
            }

            FastenResult result = station.Fasten(connection, point, fastenerSpec);
            ClearTarget();
            resultLine = result == FastenResult.Fastened
                ? (station.Build.IsComplete ? $"{station.DisplayName} complete: see the report" : null)
                : $"Not fastened: {result}";
            SetLines(null, resultLine);
        }

        private static bool HasAnyOpenPoint(AssemblyStation station) =>
            station.TryFindOpenPoint(station.transform.position, float.MaxValue, out _, out _);

        /// <summary>Caches the station's report; rebuilt only when the station reports a change, not every frame.</summary>
        private void ObserveStation(AssemblyStation station)
        {
            if (station != observedStation)
            {
                if (observedStation != null)
                {
                    observedStation.Changed -= OnStationChanged;
                }

                observedStation = station;
                observedStation.Changed += OnStationChanged;
                observedReport = null;
            }

            if (observedReport == null)
            {
                observedReport = station.Build.CreateReport();
                observedProgressText = $"{station.DisplayName}: parts {observedReport.PartsInstalled}/{observedReport.PartsRequired}, " +
                                       $"screws {observedReport.FastenersInstalled}/{observedReport.FastenersRequired}";
                observedCompleteText = $"{station.DisplayName} is complete";
                observedAimText = HasAnyOpenPoint(station)
                    ? $"{observedProgressText}\nAim at an orange screw point"
                    : $"{observedProgressText}\nInstall both parts of a joint, then aim at an orange screw point";
                observedHoldText = $"{observedProgressText}\nHold {useKeyLabel} to drive a {fastener.DisplayName}";
            }
        }

        private void OnStationChanged() => observedReport = null;

        private void OnDestroy()
        {
            if (observedStation != null)
            {
                observedStation.Changed -= OnStationChanged;
            }
        }

        private void ClearTarget()
        {
            targetStation = null;
            targetConnection = -1;
            targetPoint = -1;
            progress = 0f;
            shownPercent = -1;
            if (pickup != null)
            {
                pickup.HandOffset = Vector3.zero;
            }
        }

        private void SetLines(string status, string result)
        {
            if (status == statusLine && result == resultLine && Readout != null)
            {
                return;
            }

            statusLine = status;
            resultLine = result;
            string text = status == null ? result : result == null ? status : status + "\n" + result;
            if (text == Readout)
            {
                return;
            }

            Readout = text;
            ReadoutChanged?.Invoke();
        }
    }
}
