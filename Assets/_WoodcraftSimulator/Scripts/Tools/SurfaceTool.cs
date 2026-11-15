using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// Sanding block or oil brush in hand (Phase 9c, D-045): aim at a board (loose or installed) and hold Use. The
    /// sanding block makes a sawn board sanded; the brush oils a sanded board (oil on unsanded wood is refused).
    /// Time grows with the board's surface area.
    /// </summary>
    public sealed class SurfaceTool : MonoBehaviour, IHeldTool
    {
        [Tooltip("Sanded: the sanding block. Oiled: the brush with wood oil.")]
        [SerializeField] private BoardSurface result = BoardSurface.Sanded;

        [Tooltip("Seconds per square metre of board surface (both faces and edges).")]
        [SerializeField, Min(0.1f)] private float secondsPerSquareMeter = 8f;

        [SerializeField, Min(0.1f)] private float minimumSeconds = 1f;
        [SerializeField, Min(0.1f)] private float range = 2f;
        [SerializeField] private LayerMask mask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private InputAction useAction;
        private string useKeyLabel;
        private PickupItem pickup;
        private HeldToolContext context;
        private LoopSound workSound;
        private bool workingNow;
        private int ignoreInputFrame = -1;
        private TimberBoard target;
        private float progress;
        private string statusLine;
        private string resultLine;

        public string Readout { get; private set; }

        public event Action ReadoutChanged;

        private void Awake()
        {
            pickup = GetComponent<PickupItem>();
            useAction = InputSystem.actions.FindAction("Player/UseTool", throwIfNotFound: true);
            useKeyLabel = useAction.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
            var source = GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            // Sanding sounds like soft sawing; brushing is quiet.
            workSound = new LoopSound(source, ProceduralSounds.Saw, result == BoardSurface.Sanded ? 0.25f : 0.06f);
        }

        public void Equip(HeldToolContext toolContext)
        {
            context = toolContext;
            ignoreInputFrame = Time.frameCount;
        }

        public void Unequip()
        {
            context = null;
            target = null;
            progress = 0f;
            workingNow = false;
            workSound.Set(false);
            resultLine = null;
            SetLines(null, null);
        }

        private void LateUpdate() => workSound?.Set(workingNow && context != null);

        private void Update()
        {
            workingNow = false;
            if (context == null)
            {
                return;
            }

            Transform eyes = context.Eyes;
            TimberBoard board = null;
            if (PhysicsQueries.RaycastIgnoring(new Ray(eyes.position, eyes.forward), range, mask, context.PlayerRoot,
                    hitBuffer, out RaycastHit hit))
            {
                board = hit.collider.GetComponentInParent<TimberBoard>();
            }

            if (board != target)
            {
                target = board;
                progress = 0f;
            }

            if (board == null || board.Piece == null)
            {
                SetLines(null, resultLine);
                return;
            }

            if (!board.Piece.Species.IsTimber)
            {
                SetLines(Loc.T($"{board.Piece.Species.DisplayName} is not sanded or oiled", $"{board.Piece.Species.DisplayName} zımparalanmaz, yağlanmaz"), resultLine);
                return;
            }

            if (board.Surface >= result)
            {
                SetLines(result == BoardSurface.Sanded && board.Surface == BoardSurface.Oiled
                    ? Loc.T("Already oiled", "Zaten yağlı")
                    : result == BoardSurface.Sanded ? Loc.T("Already sanded", "Zaten zımparalı") : Loc.T("Already oiled", "Zaten yağlı"),
                    resultLine);
                return;
            }

            if (result == BoardSurface.Oiled && board.Surface == BoardSurface.Raw)
            {
                SetLines(Loc.T("Sand it first: oil on sawn wood looks rough", "Önce zımparala: kesik yüzeye yağ pürüzlü kalır"), resultLine);
                return;
            }

            bool pressed = useAction.IsPressed() && Time.frameCount != ignoreInputFrame;
            if (!pressed)
            {
                pickup.HandOffset = Vector3.zero;
                SetLines(result == BoardSurface.Sanded
                    ? Loc.T($"Hold {useKeyLabel} to sand this board", $"Bu tahtayı zımparalamak için {useKeyLabel} basılı tut")
                    : Loc.T($"Hold {useKeyLabel} to oil this board", $"Bu tahtayı yağlamak için {useKeyLabel} basılı tut"), resultLine);
                return;
            }

            BoardDimensions size = board.Piece.Dimensions;
            double area = 2.0 * (size.Length.Meters * size.Width.Meters + size.Length.Meters * size.Thickness.Meters +
                                 size.Width.Meters * size.Thickness.Meters);
            float seconds = Mathf.Max(minimumSeconds, (float)(area * secondsPerSquareMeter));
            progress += Time.deltaTime / seconds;
            workingNow = true;
            pickup.HandOffset = Vector3.right * (0.03f * Mathf.Sin(Time.time * 14f));
            if (progress < 1f)
            {
                int percent = Mathf.FloorToInt(progress * 100f);
                SetLines(result == BoardSurface.Sanded ? Loc.T($"Sanding  {percent}%", $"Zımparalanıyor  %{percent}")
                    : Loc.T($"Oiling  {percent}%", $"Yağlanıyor  %{percent}"), resultLine);
                return;
            }

            board.SetSurface(result);
            progress = 0f;
            pickup.HandOffset = Vector3.zero;
            resultLine = result == BoardSurface.Sanded ? Loc.T("Sanded", "Zımparalandı") : Loc.T("Oiled", "Yağlandı");
            SetLines(null, resultLine);
        }

        private void SetLines(string status, string outcome)
        {
            if (status == statusLine && outcome == resultLine && Readout != null)
            {
                return;
            }

            statusLine = status;
            resultLine = outcome;
            string text = status == null ? outcome : outcome == null ? status : status + "\n" + outcome;
            if (text == Readout)
            {
                return;
            }

            Readout = text;
            ReadoutChanged?.Invoke();
        }
    }
}
