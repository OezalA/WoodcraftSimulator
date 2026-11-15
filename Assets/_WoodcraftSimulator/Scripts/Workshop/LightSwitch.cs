using UnityEngine;
using WoodcraftSimulator.Interaction;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>Wall switch that toggles one or more lights. First test interactable (Phase 1).</summary>
    public sealed class LightSwitch : MonoBehaviour, IInteractable
    {
        [SerializeField] private Light[] lights;
        [SerializeField] private bool isOn = true;

        [Tooltip("Used in the prompt: \"Turn on <name>\".")]
        [SerializeField] private string lightName = "lamp";

        public string InteractionPrompt => isOn ? $"Turn off {lightName}" : $"Turn on {lightName}";

        public bool IsOn => isOn;

        private void Start() => ApplyState();

        /// <summary>Switches on or off without player interaction (loading a save).</summary>
        public void SetOn(bool on)
        {
            isOn = on;
            ApplyState();
        }

        public void Interact(PlayerInteractor interactor)
        {
            isOn = !isOn;
            ApplyState();
        }

        private void ApplyState()
        {
            foreach (Light controlledLight in lights)
            {
                if (controlledLight != null)
                {
                    controlledLight.enabled = isOn;
                }
            }
        }
    }
}
