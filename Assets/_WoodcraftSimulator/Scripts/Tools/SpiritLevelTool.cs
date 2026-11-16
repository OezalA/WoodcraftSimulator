using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// Spirit level in hand (Phase 11, D-049). Held against a face of an installed post it shows how far the post leans
    /// within that face (left/right as seen from the player); the post's other lean shows on the neighbouring face.
    /// The wheel pushes the post upright (Shift = 10 steps), the use button nails a temporary brace once the post is
    /// plumb both ways. A decision and checking task, no motor skill (D-049). The rules live in the domain
    /// (<see cref="PlumbRules"/>, <see cref="AssemblyBuild"/>).
    /// </summary>
    [RequireComponent(typeof(PickupItem))]
    public sealed class SpiritLevelTool : MonoBehaviour, IHeldTool
    {
        [Tooltip("Reach from the eyes to the post face, in meters.")]
        [SerializeField, Min(0.3f)] private float range = 1.8f;

        [SerializeField] private LayerMask mask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private HeldToolContext context;
        private InputAction useAction;
        private InputAction adjustAction;
        private InputAction coarseAction;
        private string useKeyLabel;
        private string resultLine;

        public string Readout { get; private set; }

        public event Action ReadoutChanged;

        private void Awake()
        {
            useAction = InputSystem.actions.FindAction("Player/UseTool", throwIfNotFound: true);
            adjustAction = InputSystem.actions.FindAction("Player/AdjustTool", throwIfNotFound: true);
            coarseAction = InputSystem.actions.FindAction("Player/CoarseAdjust", throwIfNotFound: true);
            useKeyLabel = useAction.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        public void Equip(HeldToolContext toolContext)
        {
            context = toolContext;
            resultLine = null;
        }

        public void Unequip()
        {
            context = null;
            SetReadout(null);
        }

        private void Update()
        {
            if (context == null)
            {
                return;
            }

            var ray = new Ray(context.Eyes.position, context.Eyes.forward);
            AssemblyStation station = null;
            int post = -1;
            if (PhysicsQueries.RaycastIgnoring(ray, range, mask, context.PlayerRoot, hitBuffer, out RaycastHit hit))
            {
                station = hit.collider.GetComponentInParent<AssemblyStation>();
                if (station != null && !station.TryGetPost(hit.collider, out post))
                {
                    station = null;
                }
            }

            if (station == null)
            {
                resultLine = null;
                SetReadout(Loc.T("Spirit level: hold it against a post", "Su terazisi: bir direğe tut"));
                return;
            }

            LeanAxis axis = station.AxisReadOnFace(hit.normal);

            // "Right" as the player sees the face: the post's +axis lean projected onto the view's right.
            float facing = Vector3.Dot(station.LeanDirection(axis), context.Eyes.right);
            int toRight = facing >= 0f ? 1 : -1;

            if (adjustAction.WasPerformedThisFrame())
            {
                float value = adjustAction.ReadValue<float>();
                if (value != 0f)
                {
                    int steps = Math.Sign(value) * (coarseAction.IsPressed() ? 10 : 1);
                    PlumbResult pushed = station.AdjustLean(post, axis, steps * toRight);
                    resultLine = pushed == PlumbResult.Braced
                        ? Loc.T("The post is braced: it doesn't move any more", "Direk destekli: artık oynamaz")
                        : null;
                }
            }

            if (useAction.WasPressedThisFrame())
            {
                resultLine = BraceText(station, post, station.Brace(post));
            }

            station.Build.TryGetLean(station.SlotId(post), out PostLean lean);
            SetReadout(Reading(station.SlotName(post), lean, axis, toRight));
        }

        private string Reading(string postName, PostLean lean, LeanAxis axis, int toRight)
        {
            int seen = lean.Along(axis) * toRight;
            string angle = PlumbRules.Format(seen);
            string line;
            if (lean.Braced)
            {
                line = Loc.T($"{postName}: braced, plumb", $"{postName}: destekli, şakülünde");
            }
            else if (PlumbRules.IsPlumb(seen))
            {
                line = Loc.T($"{postName}: bubble centred on this face ({angle}). Check the side face too.",
                    $"{postName}: bu yüzde kabarcık ortada ({angle}). Yan yüzü de kontrol et.");
            }
            else
            {
                string side = seen > 0 ? Loc.T("right", "sağa") : Loc.T("left", "sola");
                string offset = Math.Abs(PlumbRules.MillimetersPerMeter(seen)).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
                line = Loc.T($"{postName}: top leans {side} {angle} ({offset} mm per m)   [Wheel] push it upright",
                    $"{postName}: tepe {side} {angle} yatık (metrede {offset} mm)   [Tekerlek] düzelt");
            }

            if (!lean.Braced)
            {
                line += "\n" + Loc.T($"[{useKeyLabel}] Nail a temporary brace (plumb both ways)",
                    $"[{useKeyLabel}] Geçici destek çak (iki yönde de şakülünde olmalı)");
            }

            return resultLine == null ? line : $"{line}\n{resultLine}";
        }

        private static string BraceText(AssemblyStation station, int post, PlumbResult result)
        {
            switch (result)
            {
                case PlumbResult.Done:
                    return Loc.T("Braced: the post can now be fastened and the beam set on it",
                        "Destek çakıldı: direk artık sabitlenebilir, üstüne kiriş konabilir");
                case PlumbResult.Braced:
                    return Loc.T("Already braced", "Zaten destekli");
                case PlumbResult.NotPlumb:
                    station.Build.TryGetLean(station.SlotId(post), out PostLean lean);
                    bool xOff = !lean.IsPlumbAlong(LeanAxis.X);
                    bool zOff = !lean.IsPlumbAlong(LeanAxis.Z);
                    return xOff && zOff
                        ? Loc.T("Not plumb: straighten it on two neighbouring faces first", "Şakülünde değil: önce iki komşu yüzde düzelt")
                        : Loc.T("Not plumb yet: check the other face", "Henüz şakülünde değil: diğer yüzü kontrol et");
                default:
                    return Loc.T($"Not braced: {result}", $"Destek çakılamadı: {result}");
            }
        }

        private void SetReadout(string text)
        {
            if (text == Readout)
            {
                return;
            }

            Readout = text;
            ReadoutChanged?.Invoke();
        }
    }
}
