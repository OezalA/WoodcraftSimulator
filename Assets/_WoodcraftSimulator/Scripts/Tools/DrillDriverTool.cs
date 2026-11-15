using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Localization;
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

        [Tooltip("Optional: the workshop's fastener stock (screws are used up, D-040). Without it screws are unlimited.")]
        [SerializeField] private Economy.WorkshopEconomy economy;

        [Tooltip("Optional: equipment upgrades (the brushless drill drills and drives faster, D-044).")]
        [SerializeField] private Economy.WorkshopUpgrades upgrades;

        [SerializeField, Min(1f)] private float upgradedSpeedFactor = 1.6f;

        private float SpeedFactor =>
            upgrades != null && upgrades.Has(Economy.WorkshopUpgrades.BrushlessDrill) ? upgradedSpeedFactor : 1f;

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
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            motorLoop = new LoopSound(audioSource, ProceduralSounds.Drill, 0.4f);
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
            motorRunning = false;
            motorLoop?.Set(false);
            context = null;
            ClearTarget();
            bitChangeLeft = 0f;
            resultLine = null;
            SetLines(null, null);
        }

        // Sound (D-044): the motor runs while drilling or driving; a click when the clutch slips or a screw is in.
        private AudioSource audioSource;
        private LoopSound motorLoop;
        private bool motorRunning;
        private bool wasSlipping;

        private void LateUpdate() => motorLoop?.Set(motorRunning && context != null);

        private void PlayClick()
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(ProceduralSounds.Click, 0.8f);
            }
        }

        private void Update()
        {
            // Also runs while lying on the table (active GameObject); only work when held.
            motorRunning = false;
            if (context == null)
            {
                return;
            }

            HandleBitAndClutch();
            if (bitChangeLeft > 0f)
            {
                bitChangeLeft -= Time.deltaTime;
                SetLines(Loc.T($"Changing to the {BitName(bit)}...", $"{BitName(bit)} takılıyor..."), resultLine);
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

            // Nails and dowels are not driven with the drill (D-045); the drill bit still makes their holes.
            FastenerKind kind = station.Fastener(station.FastenerIdAt(connection))?.Kind ?? FastenerKind.Screw;
            if (bit == Bit.Driver && kind != FastenerKind.Screw)
            {
                pickup.HandOffset = Vector3.zero;
                string hint = kind == FastenerKind.Nail
                    ? Loc.T("This joint is nailed: use the hammer", "Bu bağlantı çivilenir: çekiç kullan")
                    : Loc.T($"This joint is doweled: drill the holes ([{modeKeyLabel}] drill bit), then glue the dowels in",
                        $"Bu bağlantı kavelalı: delikleri aç ([{modeKeyLabel}] matkap ucu), sonra kavelaları tutkalla tak");
                SetLines($"{ToolLine()}\n{hint}", resultLine);
                return;
            }

            if (!pressed)
            {
                pickup.HandOffset = Vector3.zero;
                string action = bit == Bit.Drill
                    ? Loc.T($"Hold {useKeyLabel} to drill a pilot hole", $"Pilot delik açmak için {useKeyLabel} basılı tut")
                    : DriveActionText(station, connection, point);
                if (bit == Bit.Driver && station.NeedsPilotHere(connection, point))
                {
                    // Red point (D-041): the rule is shown before the player can break it.
                    action += "\n" + Loc.T($"Red point: near the board end, drill a pilot hole first ([{modeKeyLabel}] drill bit), or the board splits",
                        $"Kırmızı nokta: tahta ucuna yakın, önce pilot delik aç ([{modeKeyLabel}] matkap ucu), yoksa tahta çatlar");
                }
                else if (bit == Bit.Driver)
                {
                    string advice = ClutchAdvice(station, connection);
                    if (advice != null)
                    {
                        action += "\n" + advice;
                    }
                }

                SetLines($"{ToolLine()}\n{action}", resultLine);
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
            pilotProgress += Time.deltaTime * SpeedFactor / pilotSeconds;
            motorRunning = true;
            pickup.HandOffset = Vector3.forward * (pushLength * Mathf.Clamp01(pilotProgress));
            if (pilotProgress < 1f)
            {
                int percent = Mathf.FloorToInt(pilotProgress * 100f);
                SetLines($"{ToolLine()}\n{Loc.T($"Drilling  {percent}%", $"Deliniyor  %{percent}")}", resultLine);
                return;
            }

            PilotResult result = targetStation.DrillPilot(targetConnection, targetPoint);
            ClearTarget();
            resultLine = result == PilotResult.Drilled
                ? Loc.T($"Pilot hole drilled: switch to the driver bit [{modeKeyLabel}]", $"Pilot delik açıldı: vida ucuna geç [{modeKeyLabel}]")
                : Loc.T($"Not drilled: {result}", $"Delinmedi: {result}");
            SetLines(ToolLine(), resultLine);
        }

        private void StartDrive()
        {
            string connectionId = targetStation.ConnectionId(targetConnection);
            driveFastener = FastenerFor(targetStation.FastenerIdAt(targetConnection));
            if (driveFastener == null)
            {
                string missing = targetStation.FastenerIdAt(targetConnection);
                SetLines($"{ToolLine()}\n{Loc.T($"No {missing} screws in the box", $"Kutuda {missing} vida yok")}", resultLine);
                return;
            }

            Length? head = targetStation.Build.HeadHeight(connectionId, targetPoint);
            driveIsNewScrew = !head.HasValue;

            // Screws are used up (D-040 K4): a new screw needs one in the workshop's stock.
            if (driveIsNewScrew && economy != null && economy.Fasteners.Count(driveFastener.Id) == 0)
            {
                SetLines($"{ToolLine()}\n" + Loc.T($"No {driveFastener.DisplayName} left: buy a box at the order board",
                    $"{driveFastener.DisplayName} kalmadı: sipariş panosundan bir kutu al"), resultLine);
                driveFastener = null;
                return;
            }

            Length startDepth = head.HasValue ? driveFastener.Length - head.Value : Length.Zero;
            drive = new ScrewDrive(driveFastener, targetStation.Build.SeatingTorque(connectionId, driveFastener), startDepth,
                driveMillimetersPerSecond: 50.0 * SpeedFactor);
            wasSlipping = false;
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
            if (drive.IsSlipping && !wasSlipping)
            {
                PlayClick();
            }

            wasSlipping = drive.IsSlipping;
            motorRunning = !drive.IsSlipping;
            targetStation.ShowDrivingHead(targetConnection, targetPoint, drive.HeadHeight);
            pickup.HandOffset = Vector3.forward * (pushLength * (float)Math.Min(1.0, drive.Depth.Millimeters / driveFastener.Length.Millimeters));

            string state = drive.IsSlipping ? Loc.T("Clutch slipping", "Tork kaydırıyor") : Loc.T("Driving", "Vidalanıyor");
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
                resultLine = Loc.T("Screw not set: hold the trigger longer", "Vida tutmadı: tetiği daha uzun basılı tut");
                SetLines(ToolLine(), resultLine);
                return;
            }

            FastenResult result = driveIsNewScrew
                ? station.Fasten(connection, point, driveFastener, finished.HeadHeight)
                : station.DriveFurther(connection, point, finished.HeadHeight);
            station.HideDrivingHead();
            if (result == FastenResult.Fastened)
            {
                PlayClick();
            }

            if (result == FastenResult.Fastened && driveIsNewScrew && economy != null)
            {
                economy.TryUseFastener(driveFastener.Id);
            }

            if (result == FastenResult.NotProud)
            {
                string head = HeadText(station.Build.HeadHeight(station.ConnectionId(connection), point) ?? Length.Zero);
                resultLine = Loc.T($"Released without driving further: {head}", $"Daha fazla girmeden bırakıldı: {head}");
            }
            else if (result != FastenResult.Fastened)
            {
                resultLine = Loc.T($"Not fastened: {result}", $"Vidalanmadı: {result}");
            }
            else
            {
                string connectionId = station.ConnectionId(connection);
                resultLine = SeatText(finished.Seat, finished.HeadHeight);
                if (station.Build.IsSplit(connectionId, point))
                {
                    resultLine += "\n" + Loc.T("The board split! Near the end of a board (red point), drill a pilot hole first",
                        "Tahta çatladı! Tahta ucuna yakın yerde (kırmızı nokta) önce pilot delik aç");
                }

                if (station.Build.IsComplete)
                {
                    resultLine += "\n" + Loc.T($"{station.DisplayName} complete", $"{station.DisplayName} tamamlandı");
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
                return Loc.T($"Hold {useKeyLabel} to drive this screw further ({HeadText(head.Value)})",
                    $"Bu vidayı daha fazla sıkmak için {useKeyLabel} basılı tut ({HeadText(head.Value)})");
            }

            FastenerSpec spec = FastenerFor(station.FastenerIdAt(connection));
            string screw = spec != null ? spec.DisplayName : Loc.T("a screw", "vida");
            return Loc.T($"Hold {useKeyLabel} to drive {screw}", $"{screw} vidalamak için {useKeyLabel} basılı tut");
        }

        /// <summary>
        /// The clutch range that stops this joint's screw flush in this wood (D-042), and whether the current setting
        /// is in it, e.g. "Recommended clutch 5-7 (Spruce, Wood screw 4 × 40 mm): now 12, too high, the head sinks".
        /// </summary>
        private string ClutchAdvice(AssemblyStation station, int connection)
        {
            FastenerSpec spec = FastenerFor(station.FastenerIdAt(connection));
            if (spec == null ||
                !ScrewDrive.TryFlushClutchRange(station.Build.SeatingTorque(station.ConnectionId(connection), spec),
                    out int lowest, out int highest))
            {
                return null;
            }

            string wood = station.TorqueWoodName(connection) ?? "?";
            string range = $"{lowest}-{highest}";
            string now = clutch == ScrewDrive.DrillMode ? Loc.T("drill mode", "delme modu") : clutch.ToString(CultureInfo.InvariantCulture);
            string verdict = clutch < lowest
                ? Loc.T("too low, the screw stops part-way", "az, vida yarıda kalır")
                : clutch <= highest
                    ? Loc.T("right", "uygun")
                    : Loc.T("too high, the head sinks", "fazla, baş gömülür");
            return Loc.T($"Recommended clutch {range} ({wood}, {spec.DisplayName}): now {now}, {verdict}",
                $"Önerilen tork {range} ({wood}, {spec.DisplayName}): şu an {now}, {verdict}");
        }

        private string AimHint(AssemblyStation station, PointQuery query)
        {
            if (station.Build.IsComplete && !station.HasPoint(PointQuery.ForScrew))
            {
                return Loc.T($"{station.DisplayName} is complete", $"{station.DisplayName} tamamlandı");
            }

            if (station.HasPoint(query))
            {
                return Loc.T("Aim at a screw point (orange: screw, red: pilot hole first)",
                    "Bir vida noktasına nişan al (turuncu: vida, kırmızı: önce pilot delik)");
            }

            return query == PointQuery.ForPilotHole
                ? Loc.T("No point to pre-drill: install both parts of a joint", "Delinecek nokta yok: bir bağlantının iki parçasını da tak")
                : Loc.T("Install both parts of a joint, then aim at a screw point", "Bir bağlantının iki parçasını tak, sonra vida noktasına nişan al");
        }

        private string ToolLine()
        {
            string clutchText = clutch == ScrewDrive.DrillMode
                ? Loc.T("drill (no clutch)", "delme (tork yok)")
                : Loc.T($"clutch {clutch}", $"tork {clutch}");
            string other = BitName(bit == Bit.Driver ? Bit.Drill : Bit.Driver);
            return Loc.T($"{BitName(bit)}, {clutchText}   [Wheel] clutch   [{modeKeyLabel}] {other}",
                $"{BitName(bit)}, {clutchText}   [Tekerlek] tork   [{modeKeyLabel}] {other}");
        }

        private static string BitName(Bit value) => value == Bit.Drill
            ? Loc.T("Drill bit 3 mm", "Matkap ucu 3 mm")
            : Loc.T("Driver bit", "Vida ucu");

        private static string HeadText(Length headHeight)
        {
            double mm = headHeight.Millimeters;
            if (Math.Abs(mm) < 0.05)
            {
                return Loc.T("head flush", "baş hizada");
            }

            string amount = Math.Abs(mm).ToString("0.0", CultureInfo.InvariantCulture);
            return mm > 0.0
                ? Loc.T($"head {amount} mm out", $"baş {amount} mm dışarıda")
                : Loc.T($"head {amount} mm deep", $"baş {amount} mm gömülü");
        }

        private static string SeatText(ScrewSeat seat, Length headHeight)
        {
            string head = HeadText(headHeight);
            switch (seat)
            {
                case ScrewSeat.Flush:
                    return Loc.T($"Screw in: {head}", $"Vida takıldı: {head}");
                case ScrewSeat.Proud:
                    return Loc.T($"Screw in, but {head}: drive it further (or raise the clutch)",
                        $"Vida takıldı ama {head}: biraz daha sık (ya da torku artır)");
                default:
                    return Loc.T($"Screw driven too deep ({head}): lower the clutch",
                        $"Vida fazla gömüldü ({head}): torku düşür");
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
