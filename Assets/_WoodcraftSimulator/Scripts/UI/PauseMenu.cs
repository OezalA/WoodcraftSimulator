using UnityEngine;
using UnityEngine.UIElements;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Persistence;
using WoodcraftSimulator.Player;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// Pause menu (UI Toolkit): resume, save, mouse sensitivity, new workshop (asks for confirmation), quit.
    /// Visibility follows <see cref="PauseController.PausedChanged"/>; layout in UI/PauseMenu.uxml.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseMenu : MonoBehaviour
    {
        private const string HiddenClass = "hidden";
        private const string NewWorkshopText = "New workshop";
        private const string ConfirmNewWorkshopText = "Really start over? Click again";

        [SerializeField] private PauseController pauseController;
        [SerializeField] private PlayerController playerController;

        [Tooltip("Optional: without it the Save / New workshop buttons are hidden.")]
        [SerializeField] private SaveGameService saveService;

        private VisualElement menuRoot;
        private Button resumeButton;
        private Button saveButton;
        private Button newButton;
        private Button quitButton;
        private Label saveStatus;
        private Slider sensitivitySlider;
        private bool confirmingNewWorkshop;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            menuRoot = root.Q<VisualElement>("pause-root");
            resumeButton = root.Q<Button>("resume-button");
            saveButton = root.Q<Button>("save-button");
            newButton = root.Q<Button>("new-button");
            quitButton = root.Q<Button>("quit-button");
            saveStatus = root.Q<Label>("save-status");
            sensitivitySlider = root.Q<Slider>("sensitivity-slider");

            if (menuRoot == null || resumeButton == null || quitButton == null || sensitivitySlider == null ||
                saveButton == null || newButton == null || saveStatus == null)
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

            bool canSave = saveService != null;
            saveButton.EnableInClassList(HiddenClass, !canSave);
            newButton.EnableInClassList(HiddenClass, !canSave);
            saveStatus.EnableInClassList(HiddenClass, !canSave);

            resumeButton.clicked += OnResumeClicked;
            saveButton.clicked += OnSaveClicked;
            newButton.clicked += OnNewClicked;
            quitButton.clicked += OnQuitClicked;
            sensitivitySlider.RegisterValueChangedCallback(OnSensitivityChanged);
            pauseController.PausedChanged += OnPausedChanged;
            if (canSave)
            {
                saveService.StatusChanged += RefreshStatus;
            }

            RefreshStatus();
            OnPausedChanged(pauseController.IsPaused);
        }

        private void OnDisable()
        {
            if (resumeButton != null)
            {
                resumeButton.clicked -= OnResumeClicked;
            }

            if (saveButton != null)
            {
                saveButton.clicked -= OnSaveClicked;
            }

            if (newButton != null)
            {
                newButton.clicked -= OnNewClicked;
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

            if (saveService != null)
            {
                saveService.StatusChanged -= RefreshStatus;
            }
        }

        private void OnPausedChanged(bool paused)
        {
            menuRoot.EnableInClassList(HiddenClass, !paused);
            ResetNewWorkshopConfirmation();

            if (!paused)
            {
                // Persist settings changed in the menu as soon as it closes.
                UserPreferences.Save();
            }
        }

        private void OnResumeClicked() => pauseController.Resume();

        private void OnSaveClicked()
        {
            ResetNewWorkshopConfirmation();
            saveService.Save();
        }

        /// <summary>First click asks, second click starts over (the current workshop is lost).</summary>
        private void OnNewClicked()
        {
            if (!confirmingNewWorkshop)
            {
                confirmingNewWorkshop = true;
                newButton.text = ConfirmNewWorkshopText;
                return;
            }

            UserPreferences.Save();
            saveService.StartNewWorkshop();
        }

        private void ResetNewWorkshopConfirmation()
        {
            confirmingNewWorkshop = false;
            if (newButton != null)
            {
                newButton.text = NewWorkshopText;
            }
        }

        private void RefreshStatus()
        {
            if (saveStatus != null && saveService != null)
            {
                saveStatus.text = saveService.LastStatus;
            }
        }

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
