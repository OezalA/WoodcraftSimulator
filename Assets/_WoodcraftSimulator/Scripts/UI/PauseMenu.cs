using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using WoodcraftSimulator.Audio;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Persistence;
using WoodcraftSimulator.Player;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// Pause menu (UI Toolkit). Main page: resume, save, settings, new workshop (asks for confirmation), quit. Settings
    /// page (Phase 13e, D-062): mouse sensitivity, invert mouse Y, field of view, interface size, language.
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

        [Tooltip("Optional: a click on buttons (Phase 13e, D-062).")]
        [SerializeField] private SoundLibrary sounds;

        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.5f;

        private VisualElement menuRoot;
        private VisualElement mainPage;
        private VisualElement settingsPage;
        private Button resumeButton;
        private Button saveButton;
        private Button settingsButton;
        private Button newButton;
        private Button quitButton;
        private Button languageButton;
        private Button backButton;
        private Label saveStatus;
        private Slider sensitivitySlider;
        private Toggle invertToggle;
        private SliderInt fieldOfViewSlider;
        private SliderInt scaleSlider;
        private AudioSource clickSource;
        private bool confirmingNewWorkshop;
        private bool scalePending;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            menuRoot = root.Q<VisualElement>("pause-root");
            mainPage = root.Q<VisualElement>("main-page");
            settingsPage = root.Q<VisualElement>("settings-page");
            resumeButton = root.Q<Button>("resume-button");
            saveButton = root.Q<Button>("save-button");
            settingsButton = root.Q<Button>("settings-button");
            newButton = root.Q<Button>("new-button");
            quitButton = root.Q<Button>("quit-button");
            languageButton = root.Q<Button>("language-button");
            backButton = root.Q<Button>("back-button");
            saveStatus = root.Q<Label>("save-status");
            sensitivitySlider = root.Q<Slider>("sensitivity-slider");
            invertToggle = root.Q<Toggle>("invert-toggle");
            fieldOfViewSlider = root.Q<SliderInt>("fov-slider");
            scaleSlider = root.Q<SliderInt>("scale-slider");
            Label title = root.Q<Label>("pause-title");
            Label settingsTitle = root.Q<Label>("settings-title");

            if (menuRoot == null || mainPage == null || settingsPage == null || resumeButton == null || saveButton == null ||
                settingsButton == null || newButton == null || quitButton == null || languageButton == null ||
                backButton == null || saveStatus == null || sensitivitySlider == null || invertToggle == null ||
                fieldOfViewSlider == null || scaleSlider == null || title == null || settingsTitle == null)
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

            if (clickSource == null)
            {
                clickSource = gameObject.AddComponent<AudioSource>();
                clickSource.playOnAwake = false;
                clickSource.spatialBlend = 0f;
                clickSource.ignoreListenerPause = true;
            }

            sensitivitySlider.lowValue = UserPreferences.MinMouseSensitivity;
            sensitivitySlider.highValue = UserPreferences.MaxMouseSensitivity;
            sensitivitySlider.showInputField = true;
            sensitivitySlider.SetValueWithoutNotify(UserPreferences.MouseSensitivity);

            invertToggle.SetValueWithoutNotify(UserPreferences.InvertLookY);

            fieldOfViewSlider.lowValue = Mathf.RoundToInt(UserPreferences.MinFieldOfView);
            fieldOfViewSlider.highValue = Mathf.RoundToInt(UserPreferences.MaxFieldOfView);
            fieldOfViewSlider.showInputField = true;
            fieldOfViewSlider.SetValueWithoutNotify(Mathf.RoundToInt(UserPreferences.FieldOfView));

            scaleSlider.lowValue = Mathf.RoundToInt(UserPreferences.MinInterfaceScale * 100f);
            scaleSlider.highValue = Mathf.RoundToInt(UserPreferences.MaxInterfaceScale * 100f);
            scaleSlider.showInputField = true;
            scaleSlider.SetValueWithoutNotify(Mathf.RoundToInt(UserPreferences.InterfaceScale * 100f));

            title.text = Loc.T("Paused", "Duraklatıldı");
            settingsTitle.text = Loc.T("Settings", "Ayarlar");
            resumeButton.text = Loc.T("Resume", "Devam et");
            saveButton.text = Loc.T("Save", "Kaydet");
            settingsButton.text = Loc.T("Settings", "Ayarlar");
            backButton.text = Loc.T("Back", "Geri");
            sensitivitySlider.label = Loc.T("Mouse sensitivity", "Fare hassasiyeti");
            invertToggle.label = Loc.T("Invert mouse Y", "Fare Y ekseni ters");
            fieldOfViewSlider.label = Loc.T("Field of view °", "Görüş açısı °");
            scaleSlider.label = Loc.T("Interface size %", "Arayüz boyutu %");
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
            settingsButton.clicked += OnSettingsClicked;
            backButton.clicked += OnBackClicked;
            newButton.clicked += OnNewClicked;
            quitButton.clicked += OnQuitClicked;
            languageButton.clicked += OnLanguageClicked;
            sensitivitySlider.RegisterValueChangedCallback(OnSensitivityChanged);
            invertToggle.RegisterValueChangedCallback(OnInvertChanged);
            fieldOfViewSlider.RegisterValueChangedCallback(OnFieldOfViewChanged);
            scaleSlider.RegisterValueChangedCallback(OnScaleChanged);
            menuRoot.RegisterCallback<ClickEvent>(OnAnyClick);
            pauseController.PausedChanged += OnPausedChanged;
            if (canSave)
            {
                saveService.StatusChanged += RefreshStatus;
            }

            RefreshStatus();
            OnPausedChanged(pauseController.IsPaused);
        }

        // After every UI document is enabled (the interface size moves them all to one scaled panel).
        private void Start() => InterfaceScale.Apply(UserPreferences.InterfaceScale);

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

            if (settingsButton != null)
            {
                settingsButton.clicked -= OnSettingsClicked;
            }

            if (backButton != null)
            {
                backButton.clicked -= OnBackClicked;
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
            invertToggle?.UnregisterValueChangedCallback(OnInvertChanged);
            fieldOfViewSlider?.UnregisterValueChangedCallback(OnFieldOfViewChanged);
            scaleSlider?.UnregisterValueChangedCallback(OnScaleChanged);
            menuRoot?.UnregisterCallback<ClickEvent>(OnAnyClick);

            if (pauseController != null)
            {
                pauseController.PausedChanged -= OnPausedChanged;
            }

            if (saveService != null)
            {
                saveService.StatusChanged -= RefreshStatus;
            }
        }

        private void Update()
        {
            // The interface size is applied once the slider is let go: resizing under a dragging mouse would move the
            // slider away from it.
            if (scalePending && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
            {
                scalePending = false;
                InterfaceScale.Apply(UserPreferences.InterfaceScale);
            }
        }

        private void OnPausedChanged(bool paused)
        {
            menuRoot.EnableInClassList(HiddenClass, !paused);
            ShowSettings(false);
            ResetNewWorkshopConfirmation();

            if (!paused)
            {
                // Persist settings changed in the menu as soon as it closes.
                UserPreferences.Save();
            }
        }

        private void ShowSettings(bool show)
        {
            mainPage.EnableInClassList(HiddenClass, show);
            settingsPage.EnableInClassList(HiddenClass, !show);
        }

        private void OnResumeClicked() => pauseController.Resume();

        private void OnSaveClicked()
        {
            ResetNewWorkshopConfirmation();
            saveService.Save();
        }

        private void OnSettingsClicked()
        {
            ResetNewWorkshopConfirmation();
            ShowSettings(true);
        }

        private void OnBackClicked()
        {
            UserPreferences.Save();
            ShowSettings(false);
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

        private void OnInvertChanged(ChangeEvent<bool> change)
        {
            UserPreferences.InvertLookY = change.newValue;
            playerController.InvertLookY = change.newValue;
        }

        private void OnFieldOfViewChanged(ChangeEvent<int> change)
        {
            UserPreferences.FieldOfView = change.newValue;
            playerController.FieldOfView = change.newValue;
        }

        private void OnScaleChanged(ChangeEvent<int> change)
        {
            UserPreferences.InterfaceScale = change.newValue / 100f;
            scalePending = true;
        }

        // One click sound for every button and the toggle (the clicked element or one of its parents).
        private void OnAnyClick(ClickEvent click)
        {
            if (sounds == null || !(click.target is VisualElement target))
            {
                return;
            }

            bool control = target.GetFirstOfType<Button>() != null || target.GetFirstOfType<Toggle>() != null;
            AudioClip clip = control ? SoundLibrary.Any(sounds.uiClick) : null;
            if (clip != null)
            {
                clickSource.PlayOneShot(clip, clickVolume);
            }
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
