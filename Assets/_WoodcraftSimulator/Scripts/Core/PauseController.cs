using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WoodcraftSimulator.Core
{
    /// <summary>
    /// Single owner of the paused state and the cursor lock.
    /// While paused: game time is frozen (Time.timeScale = 0), the cursor is free and the listed gameplay
    /// components are disabled, so they don't need to know about pausing themselves.
    /// </summary>
    public sealed class PauseController : MonoBehaviour
    {
        [Tooltip("Gameplay components disabled while paused (movement, interaction, later tools).")]
        [SerializeField] private Behaviour[] disableWhilePaused;

        [Tooltip("Pause automatically when the game window loses focus (Alt+Tab, clicking another Editor window).")]
        [SerializeField] private bool pauseOnFocusLoss = true;

        private InputAction pauseAction;

        public bool IsPaused { get; private set; }

        /// <summary>Raised with the new state whenever the game is paused or resumed.</summary>
        public event Action<bool> PausedChanged;

        private void Awake()
        {
            pauseAction = InputSystem.actions.FindAction("Player/Pause", throwIfNotFound: true);
        }

        private void Start() => ApplyState();

        private void Update()
        {
            // Input is processed every frame even when Time.timeScale is 0.
            if (pauseAction.WasPressedThisFrame())
            {
                SetPaused(!IsPaused);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && pauseOnFocusLoss)
            {
                SetPaused(true);
            }
        }

        private void OnDestroy()
        {
            // Never leave the game frozen or the cursor hidden when this object goes away (scene change, exit).
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Pause() => SetPaused(true);

        public void Resume() => SetPaused(false);

        private void SetPaused(bool paused)
        {
            if (paused == IsPaused)
            {
                return;
            }

            IsPaused = paused;
            ApplyState();
            PausedChanged?.Invoke(paused);
        }

        private void ApplyState()
        {
            Time.timeScale = IsPaused ? 0f : 1f;
            Cursor.lockState = IsPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = IsPaused;

            foreach (Behaviour behaviour in disableWhilePaused)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = !IsPaused;
                }
            }
        }
    }
}
