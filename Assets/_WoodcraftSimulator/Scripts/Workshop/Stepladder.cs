using UnityEngine;
using UnityEngine.InputSystem;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Player;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// A portable A-frame stepladder (Phase 11, D-049). It is carried like a board (E) and set down upright where it is
    /// needed; Jump (Space) while looking at it climbs onto its top standing step, Jump again climbs down. On it the
    /// player can look around, carry and use tools, but not walk. Standing ~0.95 m higher raises the working reach
    /// (<see cref="Construction.WorkReach"/>) from ~2.1 m to ~3 m above the ground.
    /// </summary>
    [RequireComponent(typeof(Carryable))]
    public sealed class Stepladder : MonoBehaviour, IClimbable, ICarryCondition, IDescribable, IInteractionHint
    {
        [Tooltip("Feet position on the top standing step, in local space (the steps face −Z).")]
        [SerializeField] private Vector3 standPoint = new Vector3(0f, 0.95f, -0.1f);

        // Upright = its up axis within ~18° of vertical.
        private const float MinUprightDot = 0.95f;

        private Carryable carryable;
        private bool occupied;
        private string climbKeyLabel;

        public Vector3 StandPosition => transform.TransformPoint(standPoint);

        public string Description => Loc.T("Stepladder", "Merdiven");

        public string InteractionHint => occupied
            ? null
            : CanClimb(out string reason)
                ? Loc.T($"[{climbKeyLabel}]  Climb the stepladder", $"[{climbKeyLabel}]  Merdivene çık")
                : reason;

        private void Awake()
        {
            carryable = GetComponent<Carryable>();
            InputAction jump = InputSystem.actions.FindAction("Player/Jump", throwIfNotFound: true);
            climbKeyLabel = jump.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        public bool CanClimb(out string reason)
        {
            reason = null;
            if (carryable.IsCarried || carryable.InstalledIn != null)
            {
                reason = Loc.T("Set the stepladder down first", "Önce merdiveni yere koy");
                return false;
            }

            if (transform.up.y < MinUprightDot)
            {
                reason = Loc.T("The stepladder must stand upright", "Merdiven dik durmalı");
                return false;
            }

            return true;
        }

        public void SetOccupied(bool value) => occupied = value;

        public bool CanPickUp(out string reason)
        {
            reason = occupied ? Loc.T("Climb down first", "Önce merdivenden in") : null;
            return !occupied;
        }
    }
}
