using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// A hand tool that puts one kind of fastener into a fastening point (Phase 9, D-045): the claw hammer drives
    /// nails (always flush; near a board end, pre-drill or it splits), the glue bottle glues dowels into drilled holes
    /// (the joint is clamped and must dry). Hold Use on a point of the right kind; one fastener comes out of the stock.
    /// </summary>
    public sealed class HandFastenTool : MonoBehaviour, IHeldTool
    {
        [SerializeField] private FastenerKind kind = FastenerKind.Nail;

        [Tooltip("Optional: the workshop's fastener stock (used up, D-040).")]
        [SerializeField] private WorkshopEconomy economy;

        [SerializeField, Min(0.1f)] private float range = 2f;
        [SerializeField, Min(0.005f)] private float pointSnapDistance = 0.03f;

        [Tooltip("Seconds of holding Use for one nail or dowel.")]
        [SerializeField, Min(0.1f)] private float seconds = 1.2f;

        [SerializeField] private LayerMask mask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private InputAction useAction;
        private string useKeyLabel;
        private PickupItem pickup;
        private HeldToolContext context;
        private AudioSource audioSource;
        private int ignoreInputFrame = -1;

        private AssemblyStation targetStation;
        private int targetConnection = -1;
        private int targetPoint = -1;
        private float progress;
        private int blowsPlayed;
        private string statusLine;
        private string resultLine;

        public string Readout { get; private set; }

        public event Action ReadoutChanged;

        private string FastenerWord => kind == FastenerKind.Nail ? Loc.T("nail", "çivi") : Loc.T("dowel", "kavela");

        private void Awake()
        {
            pickup = GetComponent<PickupItem>();
            useAction = InputSystem.actions.FindAction("Player/UseTool", throwIfNotFound: true);
            useKeyLabel = useAction.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        public void Equip(HeldToolContext toolContext)
        {
            context = toolContext;
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
            if (context == null)
            {
                return;
            }

            Transform eyes = context.Eyes;
            AssemblyStation station = null;
            if (PhysicsQueries.RaycastIgnoring(new Ray(eyes.position, eyes.forward), range, mask, context.PlayerRoot,
                    hitBuffer, out RaycastHit hit))
            {
                station = hit.collider.GetComponentInParent<AssemblyStation>();
            }

            if (station == null || station.Build == null ||
                !station.TryFindPoint(hit.point, pointSnapDistance, PointQuery.ForScrew, out int connection, out int point))
            {
                ClearTarget();
                SetLines(station != null && station.Build != null ? DryingText(station) : null, resultLine);
                return;
            }

            FastenerSpec spec = station.Fastener(station.FastenerIdAt(connection));
            if (spec == null || spec.Kind != kind)
            {
                ClearTarget();
                string needed = spec == null ? "?" : spec.Kind == FastenerKind.Screw
                    ? Loc.T("screws: use the drill-driver", "vida: şarjlı matkap kullan")
                    : spec.Kind == FastenerKind.Nail
                        ? Loc.T("nails: use the hammer", "çivi: çekiç kullan")
                        : Loc.T("dowels: use the wood glue", "kavela: ahşap tutkalı kullan");
                SetLines(Loc.T($"This joint takes {needed}", $"Bu bağlantı {needed}"), resultLine);
                return;
            }

            // Out of reach (ladder) or an unbraced post (D-049).
            string blocked = station.WorkBlockedReason(connection, context.PlayerRoot.position);
            if (blocked != null)
            {
                ClearTarget();
                SetLines(blocked, resultLine);
                return;
            }

            if (station != targetStation || connection != targetConnection || point != targetPoint)
            {
                ClearTarget();
                targetStation = station;
                targetConnection = connection;
                targetPoint = point;
            }

            string connectionId = station.ConnectionId(connection);
            if (kind == FastenerKind.Dowel && !station.Build.HasPilotHole(connectionId, point))
            {
                SetLines(Loc.T("Drill the dowel hole first (drill-driver, drill bit)", "Önce kavela deliğini aç (şarjlı matkap, matkap ucu)"),
                    resultLine);
                return;
            }

            if (economy != null && economy.Fasteners.Count(spec.Id) == 0)
            {
                SetLines(Loc.T($"No {spec.DisplayName} left: buy a box at the order board",
                    $"{spec.DisplayName} kalmadı: sipariş panosundan bir kutu al"), resultLine);
                return;
            }

            bool pressed = useAction.IsPressed() && Time.frameCount != ignoreInputFrame;
            if (!pressed)
            {
                progress = 0f;
                blowsPlayed = 0;
                pickup.HandOffset = Vector3.zero;
                string action = kind == FastenerKind.Nail
                    ? Loc.T($"Hold {useKeyLabel} to nail ({spec.DisplayName})", $"Çivilemek için {useKeyLabel} basılı tut ({spec.DisplayName})")
                    : Loc.T($"Hold {useKeyLabel} to glue a dowel in ({spec.DisplayName})", $"Kavelayı tutkallayıp takmak için {useKeyLabel} basılı tut ({spec.DisplayName})");
                if (kind == FastenerKind.Nail && station.NeedsPilotHere(connection, point))
                {
                    action += "\n" + Loc.T("Red point: near the board end, pre-drill (drill-driver) or the board splits",
                        "Kırmızı nokta: tahta ucuna yakın, önce delik aç (şarjlı matkap), yoksa tahta çatlar");
                }

                SetLines(action, resultLine);
                return;
            }

            progress += Time.deltaTime / seconds;
            if (kind == FastenerKind.Nail)
            {
                // Three blows per nail: the hand swings and each blow knocks.
                float swing = Mathf.Repeat(progress * 3f, 1f);
                pickup.HandOffset = Vector3.up * (0.06f * Mathf.Sin(swing * Mathf.PI));
                int blows = Mathf.FloorToInt(progress * 3f);
                if (blows > blowsPlayed)
                {
                    blowsPlayed = blows;
                    audioSource.PlayOneShot(ProceduralSounds.Thunk, 0.9f);
                }
            }

            if (progress < 1f)
            {
                int percent = Mathf.FloorToInt(progress * 100f);
                SetLines(kind == FastenerKind.Nail ? Loc.T($"Nailing  {percent}%", $"Çivileniyor  %{percent}")
                    : Loc.T($"Gluing  {percent}%", $"Tutkallanıyor  %{percent}"), resultLine);
                return;
            }

            Finish(station, connection, point, spec);
        }

        private void Finish(AssemblyStation station, int connection, int point, FastenerSpec spec)
        {
            FastenResult result = station.Fasten(connection, point, spec, Length.Zero);
            ClearTarget();
            if (result != FastenResult.Fastened)
            {
                resultLine = Loc.T($"Not fastened: {result}", $"Takılmadı: {result}");
                SetLines(null, resultLine);
                return;
            }

            economy?.TryUseFastener(spec.Id);
            audioSource.PlayOneShot(ProceduralSounds.Click, 0.7f);
            string connectionId = station.ConnectionId(connection);
            resultLine = kind == FastenerKind.Nail
                ? Loc.T("Nail in, flush", "Çivi çakıldı, hizada")
                : Loc.T("Dowel glued in", "Kavela tutkallanıp takıldı");
            if (station.Build.IsSplit(connectionId, point))
            {
                resultLine += "\n" + Loc.T("The board split! Near the end of a board (red point), pre-drill first",
                    "Tahta çatladı! Tahta ucuna yakın yerde (kırmızı nokta) önce delik aç");
            }

            if (station.Build.IsDrying(connectionId))
            {
                resultLine += "\n" + Loc.T("Joint clamped: the glue is drying", "Bağlantı işkencede: tutkal kuruyor");
            }

            if (station.Build.IsComplete)
            {
                resultLine += "\n" + Loc.T($"{station.DisplayName} complete", $"{station.DisplayName} tamamlandı");
            }

            SetLines(null, resultLine);
        }

        /// <summary>How long the glue of the station's joints still needs (dowel tool only).</summary>
        private string DryingText(AssemblyStation station)
        {
            if (kind != FastenerKind.Dowel)
            {
                return null;
            }

            double longest = 0.0;
            foreach (ConnectionSpec connection in station.Build.Blueprint.Connections)
            {
                longest = Math.Max(longest, station.Build.DryingSeconds(connection.Id));
            }

            return longest > 0.0
                ? Loc.T($"Glue drying: {Math.Ceiling(longest)} s", $"Tutkal kuruyor: {Math.Ceiling(longest)} sn")
                : null;
        }

        private void ClearTarget()
        {
            targetStation = null;
            targetConnection = -1;
            targetPoint = -1;
            progress = 0f;
            blowsPlayed = 0;
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
