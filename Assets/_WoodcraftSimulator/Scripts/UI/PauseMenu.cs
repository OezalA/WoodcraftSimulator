using UnityEngine;
using UnityEngine.UIElements;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Player;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// Pause menu (UI Toolkit): resume, mouse sensitivity, quit.
    /// Visibility follows <see cref="PauseController.PausedChanged"/>; layout in UI/PauseMenu.uxml.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseMenu : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        [SerializeField] private PauseController pauseController;
        [SerializeField] private PlayerController playerController;

        private VisualElement menuRoot;
        private Button resumeButton;
        private Button quitButton;
        private Slider sensitivitySlider;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            menuRoot = root.Q<VisualElement>("pause-root");
            resumeButton = root.Q<Button>("resume-button");
            quitButton = root.Q<Button>("quit-button");
            sensitivitySlider = root.Q<Slider>("sensitivity-slider");

            if (menuRoot == null || resumeButton == null || quitButton == null || sensitivitySlider == null)
            {
                Debug.LogError($"{nameof(PauseMenu)}: expected elements are missing from the UXML.", this);
                enabled = false;
                return;
            }

            if (pauseController == null || playerController == null)
            {
                Debug.LogError($"{nameof(PauseMenu)}: Pause Controller and Player Controller must be assigned.", this);
                enabled = false;
                return;
            }

            sensitivitySlider.lowValue = UserPreferences.MinMouseSensitivity;
            sensitivitySlider.highValue = UserPreferences.MaxMouseSensitivity;
            sensitivitySlider.showInputField = true;
            sensitivitySlider.SetValueWithoutNotify(UserPreferences.MouseSensitivity);

            resumeButton.clicked += OnResumeClicked;
            quitButton.clicked += OnQuitClicked;
            sensitivitySlider.RegisterValueChangedCallback(OnSensitivityChanged);
            pauseController.PausedChanged += OnPausedChanged;

            OnPausedChanged(pauseController.IsPaused);
        }

        private void OnDisable()
        {
            if (resumeButton != null)
            {
                resumeButton.clicked -= OnResumeClicked;
            }

            if (quitButton != null)
            {
                quitButton.clicked -= OnQuitClicked;
            }

            sensitivitySlider?.UnregisterValueChangedCallback(OnSensitivityChanged);

            if (pauseController != null)
            {
                pauseController.PausedChanged -= OnPausedChanged;
            }
        }

        private void OnPausedChanged(bool paused)
        {
            menuRoot.EnableInClassList(HiddenClass, !paused);

            if (!paused)
            {
                // Persist settings changed in the menu as soon as it closes.
                UserPreferences.Save();
            }
        }

        private void OnResumeClicked() => pauseController.Resume();

        private void OnSensitivityChanged(ChangeEvent<float> change)
        {
            UserPreferences.MouseSensitivity = change.newValue;
            playerController.LookSensitivity = change.newValue;
        }

        private static void OnQuitClicked()
        {
            UserPreferences.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
