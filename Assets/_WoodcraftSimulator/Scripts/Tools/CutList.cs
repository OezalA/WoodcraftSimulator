using System;
using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tools
{
    /// <summary>
    /// The cut list note on the workbench: the target dimension the pencil and saw check against. With a job in
    /// progress it holds the job's lines (e.g. "600 mm: sides"); otherwise preset practice targets. Press E on it to
    /// switch to the next line.
    /// </summary>
    public sealed class CutList : MonoBehaviour, IInteractable, IDescribable
    {
        [Tooltip("Practice targets in millimeters (no job); E switches to the next one.")]
        [SerializeField] private int[] targetsMillimeters = { 300, 450, 600, 900, 1200 };

        [SerializeField, Min(0)] private int selectedIndex = 3;

        private readonly List<Length> jobLengths = new List<Length>();
        private readonly List<string> jobLabels = new List<string>();

        private bool HasJob => jobLengths.Count > 0;

        private int Count => HasJob ? jobLengths.Count : targetsMillimeters.Length;

        /// <summary>The current target dimension.</summary>
        public Length Target => Count == 0
            ? Length.Zero
            : HasJob
                ? jobLengths[Index]
                : Length.FromMillimeters((long)targetsMillimeters[Index]);

        public string Description => Loc.T($"Cut list: target {Target}", $"Kesim listesi: hedef {Target}");

        // The HUD shows only this line for interactables, so it states the CURRENT target (showing the next one
        // was misread as the current one, D-028).
        public string InteractionPrompt => HasJob
            ? Loc.T($"Next cut list line   (now: {Target}, {jobLabels[Index]})", $"Sonraki kesim satırı   (şu an: {Target}, {jobLabels[Index]})")
            : Loc.T($"Change target   (now: {Target})", $"Hedefi değiştir   (şu an: {Target})");

        /// <summary>Raised after the target changed.</summary>
        public event Action TargetChanged;

        /// <summary>Index of the current line (saved with the game).</summary>
        public int SelectedIndex => selectedIndex;

        private int Index => Count == 0 ? 0 : Mathf.Abs(selectedIndex) % Count;

        /// <summary>The job's cut list lines replace the practice targets (empty = back to practice).</summary>
        public void SetJobLines(IReadOnlyList<Length> lengths, IReadOnlyList<string> labels)
        {
            jobLengths.Clear();
            jobLabels.Clear();
            for (int i = 0; i < lengths.Count; i++)
            {
                jobLengths.Add(lengths[i]);
                jobLabels.Add(labels != null && i < labels.Count ? labels[i] : string.Empty);
            }

            selectedIndex = 0;
            TargetChanged?.Invoke();
        }

        public void ClearJobLines() => SetJobLines(Array.Empty<Length>(), null);

        /// <summary>Sets the current line (loading a save). Out-of-range indices wrap around the list.</summary>
        public void Restore(int index)
        {
            if (Count == 0)
            {
                return;
            }

            selectedIndex = Mathf.Abs(index) % Count;
            TargetChanged?.Invoke();
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (Count == 0)
            {
                return;
            }

            selectedIndex = (Index + 1) % Count;
            TargetChanged?.Invoke();
        }
    }
}
