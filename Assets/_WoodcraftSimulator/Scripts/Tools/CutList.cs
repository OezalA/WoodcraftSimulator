using System;
using UnityEngine;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// A note on the workbench holding the target dimension to mark (prototype stand-in for a job's cut list,
    /// Phase 6). Press E on it to switch to the next preset target.
    /// </summary>
    public sealed class CutList : MonoBehaviour, IInteractable, IDescribable
    {
        [Tooltip("Target dimensions in millimeters; E switches to the next one.")]
        [SerializeField] private int[] targetsMillimeters = { 300, 450, 600, 900, 1200 };

        [SerializeField, Min(0)] private int selectedIndex = 3;

        /// <summary>The current target dimension.</summary>
        public Length Target => Length.FromMillimeters((long)CurrentMillimeters(selectedIndex));

        public string Description => $"Cut list: target {Target}";

        public string InteractionPrompt => $"Next target ({CurrentMillimeters(selectedIndex + 1)} mm)";

        /// <summary>Raised after the target changed.</summary>
        public event Action TargetChanged;

        public void Interact(PlayerInteractor interactor)
        {
            if (targetsMillimeters.Length == 0)
            {
                return;
            }

            selectedIndex = (selectedIndex + 1) % targetsMillimeters.Length;
            TargetChanged?.Invoke();
        }

        private int CurrentMillimeters(int index) =>
            targetsMillimeters.Length == 0 ? 0 : targetsMillimeters[Mathf.Abs(index) % targetsMillimeters.Length];
    }
}
