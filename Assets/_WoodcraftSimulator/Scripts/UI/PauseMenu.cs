using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Persistence;
using WoodcraftSimulator.Player;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// Pause menu (UI Toolkit): resume, save, mouse sensitivity, language, new workshop (asks for confirmation), quit.
    /// Visibility follows <see cref="PauseController.PausedChanged"/>; layout in UI/PauseMenu.uxml.
    /// Texts are set here in the current language (D-041); the UXML only holds English placeholders.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseMenu : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        private static string NewWorkshopText => Loc.T("New workshop", "Yeni atölye");

        private static string ConfirmNewWorkshopText => Loc.T("Really start over? Click again", "Gerçekten baştan başlansın mı? Tekrar tıkla");

        [SerializeField] private PauseController pauseController;
        [SerializeField] private PlayerController playerController;

        [Tooltip("Optional: without it the Save / New workshop buttons are hidden.")]
        [SerializeField] private SaveGameService saveService;

        private VisualElement menuRoot;
        private Button resumeButton;
        private Button saveButton;
        private Button newButton;
        private Button quitButton;
        private Button languageButton;
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
            languageButton = root.Q<Button>("language-button");
            saveStatus = root.Q<Label>("save-status");
            sensitivitySlider = root.Q<Slider>("sensitivity-slider");
            Label title = root.Q<Label>("pause-title");

            if (menuRoot == null || resumeButton == null || quitButton == null || sensitivitySlider == null ||
                saveButton == null || newButton == null || saveStatus == null || languageButton == null || title == null)
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

            title.text = Loc.T("Paused", "Duraklatıldı");
            resumeButton.text = Loc.T("Resume", "Devam et");
            saveButton.text = Loc.T("Save", "Kaydet");
            sensitivitySlider.label = Loc.T("Mouse sensitivity", "Fare hassasiyeti");
            quitButton.text = Loc.T("Quit", "Çıkış");
            // The button names the language it switches to, in that language.
            languageButton.text = Loc.IsTurkish ? "Language: English" : "Dil: Türkçe";
            newButton.text = NewWorkshopText;

            bool canSave = saveService != null;
            saveButton.EnableInClassList(HiddenClass, !canSave);
            newButton.EnableInClassList(HiddenClass, !canSave);
            saveStatus.EnableInClassList(HiddenClass, !canSave);

            resumeButton.clicked += OnResumeClicked;
            saveButton.clicked += OnSaveClicked;
            newButton.clicked += OnNewClicked;
            quitButton.clicked += OnQuitClicked;
            languageButton.clicked += OnLanguageClicked;
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

            if (languageButton != null)
            {
                languageButton.clicked -= OnLanguageClicked;
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

        /// <summary>
        /// Switches the language. Names of species, parts and jobs are built when the scene loads, so the workshop is
        /// saved and the scene reloaded (nothing is lost).
        /// </summary>
        private void OnLanguageClicked()
        {
            UserPreferences.Language = Loc.IsTurkish ? Language.English : Language.Turkish;
            UserPreferences.Save();
            Loc.Current = UserPreferences.Language;
            if (saveService != null)
            {
                saveService.SaveAndReload();
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
