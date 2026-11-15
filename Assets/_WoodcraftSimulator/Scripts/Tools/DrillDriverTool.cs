using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// Cordless drill-driver in hand (D-038). Two bits, switched with ToolMode (Q): the drill bit pre-drills a pilot hole
    /// at an orange fastening point (hold Use), the driver bit drives a screw of the type the joint needs (hold Use;
    /// the screw advances while held, see <see cref="ScrewDrive"/>). The wheel sets the clutch (1–20, then drill mode):
    /// the right setting stops the screw flush by itself. Releasing early leaves the head proud; aim at it and drive it
    /// further. Screws are unlimited until the material economy (Phase 7).
    /// </summary>
    public sealed class DrillDriverTool : MonoBehaviour, IHeldTool
    {
        private enum Bit
        {
            Driver,
            Drill
        }

        [Tooltip("Screw types in the box (the joint's blueprint names the one it needs).")]
        [SerializeField] private FastenerDefinition[] fasteners = Array.Empty<FastenerDefinition>();

        [SerializeField, Min(0.1f)] private float range = 2f;

        [Tooltip("The tool snaps to a fastening point within this distance (m) of the aimed point.")]
        [SerializeField, Min(0.005f)] private float pointSnapDistance = 0.03f;

        [Tooltip("Seconds of drilling for one pilot hole.")]
        [SerializeField, Min(0.1f)] private float pilotSeconds = 0.6f;

        [Tooltip("Seconds to change between the driver bit and the drill bit.")]
        [SerializeField, Min(0f)] private float bitChangeSeconds = 1f;

        [SerializeField, Range(ScrewDrive.MinClutch, ScrewDrive.DrillMode)] private int clutch = 12;

        [Tooltip("A new screw driven less than this (mm) is not set: it is taken out again.")]
        [SerializeField, Min(0f)] private float minimumSetDepthMillimeters = 3f;

        [SerializeField] private LayerMask mask = Physics.DefaultRaycastLayers;

        [Header("Push animation (camera space)")]
        [SerializeField, Min(0f)] private float pushLength = 0.02f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private InputAction useAction;
        private InputAction modeAction;
        private InputAction adjustAction;
        private string useKeyLabel;
        private string modeKeyLabel;
        private PickupItem pickup;
        private HeldToolContext context;
        private int ignoreInputFrame = -1;

        private Bit bit = Bit.Driver;
        private float bitChangeLeft;

        private AssemblyStation targetStation;
        private int targetConnection = -1;
        private int targetPoint = -1;
        private float pilotProgress;
        private ScrewDrive drive;
        private bool driveIsNewScrew;
        private FastenerSpec driveFastener;

        private string statusLine;
        private string resultLine;

        public string Readout { get; private set; }

        public event Action ReadoutChanged;

        private void Awake()
        {
            pickup = GetComponent<PickupItem>();
            if (fasteners.Length == 0)
            {
                Debug.LogError($"{nameof(DrillDriverTool)} '{name}': no fastener definitions assigned.", this);
                enabled = false;
                return;
            }

            useAction = InputSystem.actions.FindAction("Player/UseTool", throwIfNotFound: true);
            modeAction = InputSystem.actions.FindAction("Player/ToolMode", throwIfNotFound: true);
            adjustAction = InputSystem.actions.FindAction("Player/AdjustTool", throwIfNotFound: true);
            useKeyLabel = KeyLabel(useAction);
            modeKeyLabel = KeyLabel(modeAction);
        }

        private static string KeyLabel(InputAction action) =>
            action.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);

        public void Equip(HeldToolContext toolContext)
        {
            context = toolContext;
            // The click that resumes from the pause menu must not start driving.
            ignoreInputFrame = Time.frameCount;
        }

        public void Unequip()
        {
            FinishDrive();
            context = null;
            ClearTarget();
            bitChangeLeft = 0f;
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

            HandleBitAndClutch();
            if (bitChangeLeft > 0f)
            {
                bitChangeLeft -= Time.deltaTime;
                SetLines($"Changing to the {BitName(bit)}...", resultLine);
                return;
            }

            bool pressed = useAction.IsPressed() && Time.frameCount != ignoreInputFrame;
            if (drive != null && !pressed)
            {
                FinishDrive();
            }

            Transform eyes = context.Eyes;
            var ray = new Ray(eyes.position, eyes.forward);
            AssemblyStation station = null;
            if (PhysicsQueries.RaycastIgnoring(ray, range, mask, context.PlayerRoot, hitBuffer, out RaycastHit hit))
            {
                station = hit.collider.GetComponentInParent<AssemblyStation>();
            }

            // While a screw is being driven the tool stays on it.
            if (drive != null)
            {
                Drive();
                return;
            }

            PointQuery query = bit == Bit.Drill ? PointQuery.ForPilotHole : PointQuery.ForScrew;
            if (station == null || station.Build == null)
            {
                ClearTarget();
                SetLines(ToolLine(), resultLine);
                return;
            }

            if (!station.TryFindPoint(hit.point, pointSnapDistance, query, out int connection, out int point))
            {
                ClearTarget();
                SetLines($"{ToolLine()}\n{AimHint(station, query)}", resultLine);
                return;
            }

            if (station != targetStation || connection != targetConnection || point != targetPoint)
            {
                ClearTarget();
                targetStation = station;
                targetConnection = connection;
                targetPoint = point;
            }

            if (!pressed)
            {
                pickup.HandOffset = Vector3.zero;
                string action = bit == Bit.Drill ? "drill a pilot hole" : DriveActionText(station, connection, point);
                SetLines($"{ToolLine()}\nHold {useKeyLabel} to {action}", resultLine);
                return;
            }

            if (bit == Bit.Drill)
            {
                DrillPilot();
            }
            else
            {
                StartDrive();
            }
        }

        private void HandleBitAndClutch()
        {
            if (drive == null && pilotProgress <= 0f && modeAction.WasPressedThisFrame())
            {
                bit = bit == Bit.Driver ? Bit.Drill : Bit.Driver;
                bitChangeLeft = bitChangeSeconds;
                ClearTarget();
            }

            if (adjustAction.WasPerformedThisFrame())
            {
                float value = adjustAction.ReadValue<float>();
                if (value != 0f)
                {
                    clutch = Mathf.Clamp(clutch + Math.Sign(value), ScrewDrive.MinClutch, ScrewDrive.DrillMode);
                }
            }
        }

        private void DrillPilot()
        {
            pilotProgress += Time.deltaTime / pilotSeconds;
            pickup.HandOffset = Vector3.forward * (pushLength * Mathf.Clamp01(pilotProgress));
            if (pilotProgress < 1f)
            {
                SetLines($"{ToolLine()}\nDrilling  {Mathf.FloorToInt(pilotProgress * 100f)}%", resultLine);
                return;
            }

            PilotResult result = targetStation.DrillPilot(targetConnection, targetPoint);
            ClearTarget();
            resultLine = result == PilotResult.Drilled ? "Pilot hole drilled" : $"Not drilled: {result}";
            SetLines(ToolLine(), resultLine);
        }

        private void StartDrive()
        {
            string connectionId = targetStation.ConnectionId(targetConnection);
            driveFastener = FastenerFor(targetStation.FastenerIdAt(targetConnection));
            if (driveFastener == null)
            {
                SetLines($"{ToolLine()}\nNo {targetStation.FastenerIdAt(targetConnection)} screws in the box", resultLine);
                return;
            }

            Length? head = targetStation.Build.HeadHeight(connectionId, targetPoint);
            driveIsNewScrew = !head.HasValue;
            Length startDepth = head.HasValue ? driveFastener.Length - head.Value : Length.Zero;
            drive = new ScrewDrive(driveFastener, targetStation.Build.SeatingTorque(connectionId, driveFastener), startDepth);
            resultLine = null;
            Drive();
        }

        private void Drive()
        {
            if (targetStation == null)
            {
                drive = null;
                return;
            }

            drive.Advance(Time.deltaTime, clutch);
            targetStation.ShowDrivingHead(targetConnection, targetPoint, drive.HeadHeight);
            pickup.HandOffset = Vector3.forward * (pushLength * (float)Math.Min(1.0, drive.Depth.Millimeters / driveFastener.Length.Millimeters));

            string state = drive.IsSlipping ? "Clutch slipping" : "Driving";
            SetLines($"{ToolLine()}\n{state}: {HeadText(drive.HeadHeight)}", resultLine);
        }

        /// <summary>Trigger released: the screw stays where it is (or comes out again if it isn't set).</summary>
        private void FinishDrive()
        {
            if (drive == null)
            {
                return;
            }

            ScrewDrive finished = drive;
            AssemblyStation station = targetStation;
            int connection = targetConnection;
            int point = targetPoint;
            drive = null;
            ClearTarget();
            if (station == null)
            {
                return;
            }

            if (driveIsNewScrew && finished.Depth.Millimeters < minimumSetDepthMillimeters)
            {
                station.HideDrivingHead();
                resultLine = "Screw not set: hold the trigger longer";
                SetLines(ToolLine(), resultLine);
                return;
            }

            FastenResult result = driveIsNewScrew
                ? station.Fasten(connection, point, driveFastener, finished.HeadHeight)
                : station.DriveFurther(connection, point, finished.HeadHeight);
            station.HideDrivingHead();

            if (result == FastenResult.NotProud)
            {
                resultLine = $"Released without driving further: {HeadText(station.Build.HeadHeight(station.ConnectionId(connection), point) ?? Length.Zero)}";
            }
            else if (result != FastenResult.Fastened)
            {
                resultLine = $"Not fastened: {result}";
            }
            else
            {
                string connectionId = station.ConnectionId(connection);
                resultLine = SeatText(finished.Seat, finished.HeadHeight);
                if (station.Build.IsSplit(connectionId, point))
                {
                    resultLine += "\nThe board split! Near the end of a board, drill a pilot hole first";
                }

                if (station.Build.IsComplete)
                {
                    resultLine += $"\n{station.DisplayName} complete";
                }
            }

            SetLines(ToolLine(), resultLine);
        }

        private FastenerSpec FastenerFor(string fastenerId)
        {
            foreach (FastenerDefinition definition in fasteners)
            {
                if (definition != null && definition.Id == fastenerId)
                {
                    return definition.ToDomain();
                }
            }

            return null;
        }

        private string DriveActionText(AssemblyStation station, int connection, int point)
        {
            Length? head = station.Build.HeadHeight(station.ConnectionId(connection), point);
            if (head.HasValue)
            {
                return $"drive this screw further ({HeadText(head.Value)})";
            }

            FastenerSpec spec = FastenerFor(station.FastenerIdAt(connection));
            return spec != null ? $"drive a {spec.DisplayName}" : "drive a screw";
        }

        private string AimHint(AssemblyStation station, PointQuery query)
        {
            if (station.Build.IsComplete && !station.HasPoint(PointQuery.ForScrew))
            {
                return $"{station.DisplayName} is complete";
            }

            if (station.HasPoint(query))
            {
                return "Aim at an orange screw point";
            }

            return query == PointQuery.ForPilotHole
                ? "No point to pre-drill: install both parts of a joint"
                : "Install both parts of a joint, then aim at an orange screw point";
        }

        private string ToolLine()
        {
            string clutchText = clutch == ScrewDrive.DrillMode ? "drill (no clutch)" : $"clutch {clutch}";
            return $"{BitName(bit)}, {clutchText}   [Wheel] clutch   [{modeKeyLabel}] {BitName(bit == Bit.Driver ? Bit.Drill : Bit.Driver)}";
        }

        private static string BitName(Bit value) => value == Bit.Drill ? "Drill bit 3 mm" : "Driver bit";

        private static string HeadText(Length headHeight)
        {
            double mm = headHeight.Millimeters;
            if (Math.Abs(mm) < 0.05)
            {
                return "head flush";
            }

            string amount = Math.Abs(mm).ToString("0.0", CultureInfo.InvariantCulture);
            return mm > 0.0 ? $"head {amount} mm out" : $"head {amount} mm deep";
        }

        private static string SeatText(ScrewSeat seat, Length headHeight)
        {
            switch (seat)
            {
                case ScrewSeat.Flush:
                    return $"Screw in: {HeadText(headHeight)}";
                case ScrewSeat.Proud:
                    return $"Screw in, but {HeadText(headHeight)}: drive it further (or raise the clutch)";
                default:
                    return $"Screw driven too deep ({HeadText(headHeight)}): lower the clutch";
            }
        }

        private void ClearTarget()
        {
            targetStation = null;
            targetConnection = -1;
            targetPoint = -1;
            pilotProgress = 0f;
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
