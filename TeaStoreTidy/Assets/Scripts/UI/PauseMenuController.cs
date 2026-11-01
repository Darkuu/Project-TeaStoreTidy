using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    private static PauseMenuController instance;

    private const string VolumeKey = "MasterVolume";
    private const string QualityKey = "Quality";
    private const string FullscreenKey = "Fullscreen";

    // Dimensions are more reliable than a resolution-list index.
    private const string ResolutionWidthKey = "ResolutionWidth";
    private const string ResolutionHeightKey = "ResolutionHeight";

    [Header("Menu")]
    [SerializeField] private GameObject menuRoot;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button quitToMenuButton;
    [SerializeField] private Button quitToDesktopButton;

    [Header("Settings Panels")]
    [Tooltip("The panel containing Resume, Save, Settings and Quit. " +
             "This must be a child of Menu Root, not Menu Root itself.")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("The settings panel. This must also be inside Menu Root.")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings Navigation")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backFromSettingsButton;

    [Header("Settings Controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Audio")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParameter = "MasterVolume";

    [Header("References — Auto-found in parents if empty")]
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameSaveSystem saveSystem;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    private readonly List<Vector2Int> availableResolutions =
        new List<Vector2Int>();

    private bool isPaused;
    private bool settingsOpen;
    private bool settingsInitialized;
    private bool preferencesDirty;
    private bool settingsPanelsValid;

    private int lastStateChangeFrame = -1;
    private int lastNavigationFrame = -1;

    private float previousTimeScale;
    private bool playerWasEnabled;
    private bool inventoryWasEnabled;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private int desiredWidth;
    private int desiredHeight;
    private bool desiredFullscreen;

    public bool IsPaused => isPaused;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (menuRoot == null)
        {
            Debug.LogError("Assign the pause menu's Menu Root.", this);
            enabled = false;
            return;
        }

        // The controller must remain active while the menu is hidden.
        if (transform.IsChildOf(menuRoot.transform))
        {
            Debug.LogError(
                "PauseMenuController must be outside Menu Root. " +
                "Put it on an always-active parent or separate object.",
                this);

            enabled = false;
            return;
        }

        if (instance != null && instance != this)
        {
            Debug.LogWarning(
                "Duplicate PauseMenuController detected. " +
                "Only one pause controller may handle Escape.",
                this);

            // Avoid hiding the valid controller's menu if shared.
            if (menuRoot != instance.menuRoot)
                menuRoot.SetActive(false);

            enabled = false;
            return;
        }

        instance = this;

        if (playerController == null)
            playerController = GetComponentInParent<FirstPersonController>();

        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();

        if (saveSystem == null)
            saveSystem = GetComponentInParent<GameSaveSystem>();

        Bind(resumeButton, Resume);
        Bind(saveGameButton, SaveGame);
        Bind(quitToMenuButton, QuitToMenu);
        Bind(quitToDesktopButton, QuitToDesktop);

        Bind(settingsButton, OpenSettings);
        Bind(backFromSettingsButton, CloseSettings);

        if (saveGameButton != null)
            saveGameButton.interactable = saveSystem != null;

        settingsPanelsValid = ValidateSettingsPanels();

        if (settingsButton != null)
            settingsButton.interactable = settingsPanelsValid;

        if (settingsPanelsValid)
            settingsPanel.SetActive(false);

        menuRoot.SetActive(false);
    }

    private void Start()
    {
        if (instance != this)
            return;

        // AudioMixer.SetFloat should be called in Start or later.
        InitializeSettings();
    }

    private bool ValidateSettingsPanels()
    {
        if (settingsPanel == null)
        {
            Debug.LogWarning(
                "Assign Settings Root to the Settings Panel field.",
                this);

            return false;
        }

        if (settingsPanel == menuRoot ||
            settingsPanel.transform.IsChildOf(menuRoot.transform) ||
            menuRoot.transform.IsChildOf(settingsPanel.transform) ||
            transform.IsChildOf(settingsPanel.transform))
        {
            Debug.LogError(
                "Menu Root and Settings Root must be separate panels. " +
                "Place PauseMenuController on their always-active parent.",
                this);

            return false;
        }

        return true;
    }

    private void Update()
    {
        if (instance != this)
            return;

        if (Keyboard.current == null ||
            !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        // Prevent Escape and a UI callback from navigating twice.
        if (lastNavigationFrame == Time.frameCount)
            return;

        if (isPaused && settingsOpen)
        {
            CloseSettings();
            return;
        }

        SetPaused(!isPaused);
    }

    private void OnDisable()
    {
        if (instance != this)
            return;

        if (isPaused)
            ApplyPauseState(false);

        FlushPreferences();
    }

    private void OnDestroy()
    {
        Unbind(resumeButton, Resume);
        Unbind(saveGameButton, SaveGame);
        Unbind(quitToMenuButton, QuitToMenu);
        Unbind(quitToDesktopButton, QuitToDesktop);

        Unbind(settingsButton, OpenSettings);
        Unbind(backFromSettingsButton, CloseSettings);

        if (settingsInitialized)
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.RemoveListener(
                    SetMasterVolume);
            }

            if (qualityDropdown != null)
                qualityDropdown.onValueChanged.RemoveListener(SetQuality);

            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.RemoveListener(SetResolution);

            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
        }

        if (instance != this)
            return;

        if (isPaused)
            ApplyPauseState(false);

        FlushPreferences();
        instance = null;
    }

    private void OnApplicationPause(bool paused)
    {
        if (instance == this && paused)
            FlushPreferences();
    }

    private void OnApplicationQuit()
    {
        if (instance == this)
            FlushPreferences();
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void Unbind(Button button, UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    public void SetPaused(bool paused)
    {
        if (instance != this || !isActiveAndEnabled)
            return;

        if (isPaused == paused)
            return;

        if (lastStateChangeFrame == Time.frameCount)
            return;

        lastStateChangeFrame = Time.frameCount;
        lastNavigationFrame = Time.frameCount;

        ApplyPauseState(paused);
    }

    private void ApplyPauseState(bool paused)
    {
        if (isPaused == paused)
            return;

        isPaused = paused;

        if (paused)
        {
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;

            playerWasEnabled =
                playerController != null && playerController.enabled;

            inventoryWasEnabled =
                inventory != null && inventory.enabled;

            if (playerController != null)
                playerController.enabled = false;

            if (inventory != null)
                inventory.enabled = false;

            Time.timeScale = 0f;

            ShowPausePanel();
            menuRoot.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            ShowPausePanel();
            FlushPreferences();

            if (menuRoot != null)
                menuRoot.SetActive(false);

            Time.timeScale = previousTimeScale;

            if (playerController != null)
                playerController.enabled = playerWasEnabled;

            if (inventory != null)
                inventory.enabled = inventoryWasEnabled;

            // Restore after enabling the player, because its OnEnable
            // may also change the cursor.
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
    }

    private void ShowPausePanel()
    {
        settingsOpen = false;

        if (settingsPanelsValid && settingsPanel != null)
            settingsPanel.SetActive(false);

        if (menuRoot != null)
            menuRoot.SetActive(isPaused);
    }

    public void OpenSettings()
    {
        if (instance != this ||
            !isActiveAndEnabled ||
            !isPaused ||
            !settingsPanelsValid)
        {
            return;
        }

        InitializeSettings();
        RefreshSettingsControls();

        settingsOpen = true;
        lastNavigationFrame = Time.frameCount;

        menuRoot.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (instance != this || !isPaused || !settingsOpen)
            return;

        lastNavigationFrame = Time.frameCount;

        ShowPausePanel();
        FlushPreferences();
    }

    private void InitializeSettings()
    {
        if (settingsInitialized)
            return;

        settingsInitialized = true;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.wholeNumbers = false;

            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(
                new List<string>(QualitySettings.names));

            qualityDropdown.interactable =
                QualitySettings.names.Length > 0;

            qualityDropdown.onValueChanged.AddListener(SetQuality);
        }

        BuildResolutionOptions();

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(SetResolution);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

        ApplySavedSettings();
        RefreshSettingsControls();
    }

    private void BuildResolutionOptions()
    {
        availableResolutions.Clear();

        foreach (Resolution resolution in Screen.resolutions)
            AddResolution(resolution.width, resolution.height);

        // Screen.resolutions may be empty, or omit the current
        // windowed size.
        AddResolution(Screen.width, Screen.height);

        if (resolutionDropdown == null)
            return;

        var labels = new List<string>();

        foreach (Vector2Int resolution in availableResolutions)
            labels.Add($"{resolution.x} x {resolution.y}");

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);

        resolutionDropdown.interactable =
            availableResolutions.Count > 0;
    }

    private void AddResolution(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        Vector2Int size = new Vector2Int(width, height);

        if (!availableResolutions.Contains(size))
            availableResolutions.Add(size);
    }

    private void ApplySavedSettings()
    {
        ApplyMasterVolume(
            Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f)));

        if (QualitySettings.names.Length > 0)
        {
            int index = Mathf.Clamp(
                PlayerPrefs.GetInt(
                    QualityKey,
                    QualitySettings.GetQualityLevel()),
                0,
                QualitySettings.names.Length - 1);

            QualitySettings.SetQualityLevel(index, true);
        }

        desiredWidth = Screen.width;
        desiredHeight = Screen.height;

        desiredFullscreen =
            PlayerPrefs.GetInt(
                FullscreenKey,
                Screen.fullScreen ? 1 : 0) != 0;

        bool hasSavedDimensions =
            PlayerPrefs.HasKey(ResolutionWidthKey) &&
            PlayerPrefs.HasKey(ResolutionHeightKey);

        bool hasLegacyResolution = PlayerPrefs.HasKey("Resolution");

        if (hasSavedDimensions)
        {
            Vector2Int savedSize = new Vector2Int(
                PlayerPrefs.GetInt(ResolutionWidthKey),
                PlayerPrefs.GetInt(ResolutionHeightKey));

            // Ignore unsupported resolutions from a previous display.
            if (availableResolutions.Contains(savedSize))
            {
                desiredWidth = savedSize.x;
                desiredHeight = savedSize.y;
            }
        }
        else if (hasLegacyResolution && availableResolutions.Count > 0)
        {
            // Best-effort compatibility with your main menu's
            // existing index-based resolution preference.
            int index = Mathf.Clamp(
                PlayerPrefs.GetInt("Resolution"),
                0,
                availableResolutions.Count - 1);

            desiredWidth = availableResolutions[index].x;
            desiredHeight = availableResolutions[index].y;
        }

        if (hasSavedDimensions ||
            hasLegacyResolution ||
            PlayerPrefs.HasKey(FullscreenKey))
        {
            ApplyDisplaySettings();
        }
    }

    private void RefreshSettingsControls()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(
                Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f)));
        }

        if (qualityDropdown != null &&
            QualitySettings.names.Length > 0)
        {
            qualityDropdown.SetValueWithoutNotify(
                QualitySettings.GetQualityLevel());

            qualityDropdown.RefreshShownValue();
        }

        if (resolutionDropdown != null &&
            availableResolutions.Count > 0)
        {
            int index = availableResolutions.IndexOf(
                new Vector2Int(desiredWidth, desiredHeight));

            if (index < 0)
                index = 0;

            resolutionDropdown.SetValueWithoutNotify(index);
            resolutionDropdown.RefreshShownValue();
        }

        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(desiredFullscreen);
    }

    private void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);

        ApplyMasterVolume(value);

        PlayerPrefs.SetFloat(VolumeKey, value);
        preferencesDirty = true;
    }

    private void ApplyMasterVolume(float value)
    {
        if (audioMixer != null &&
            !string.IsNullOrWhiteSpace(masterVolumeParameter))
        {
            float decibels =
                value <= 0.0001f
                    ? -80f
                    : Mathf.Log10(value) * 20f;

            if (!audioMixer.SetFloat(masterVolumeParameter, decibels))
            {
                Debug.LogWarning(
                    $"AudioMixer parameter '{masterVolumeParameter}' " +
                    "was not found. Make sure it is exposed.",
                    this);
            }
        }
        else
        {
            AudioListener.volume = value;
        }
    }

    private void SetQuality(int index)
    {
        if (index < 0 || index >= QualitySettings.names.Length)
            return;

        QualitySettings.SetQualityLevel(index, true);

        PlayerPrefs.SetInt(QualityKey, index);
        preferencesDirty = true;
    }

    private void SetResolution(int index)
    {
        if (index < 0 || index >= availableResolutions.Count)
            return;

        desiredWidth = availableResolutions[index].x;
        desiredHeight = availableResolutions[index].y;

        ApplyDisplaySettings();
        StoreDisplayPreferences();
    }

    private void SetFullscreen(bool fullscreen)
    {
        desiredFullscreen = fullscreen;

        ApplyDisplaySettings();
        StoreDisplayPreferences();
    }

    private void ApplyDisplaySettings()
    {
        if (desiredWidth <= 0 || desiredHeight <= 0)
            return;

        // Resolution changes are applied asynchronously, so retain
        // the requested dimensions instead of reading Screen.width
        // immediately after a change.
        Screen.SetResolution(
            desiredWidth,
            desiredHeight,
            desiredFullscreen);
    }

    private void StoreDisplayPreferences()
    {
        PlayerPrefs.SetInt(ResolutionWidthKey, desiredWidth);
        PlayerPrefs.SetInt(ResolutionHeightKey, desiredHeight);
        PlayerPrefs.SetInt(FullscreenKey, desiredFullscreen ? 1 : 0);

        // Keep your unchanged main menu compatible.
        int index = availableResolutions.IndexOf(
            new Vector2Int(desiredWidth, desiredHeight));

        if (index >= 0)
            PlayerPrefs.SetInt("Resolution", index);

        preferencesDirty = true;
    }

    private void FlushPreferences()
    {
        if (!preferencesDirty)
            return;

        PlayerPrefs.Save();
        preferencesDirty = false;
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void SaveGame()
    {
        if (instance != this ||
            !isActiveAndEnabled ||
            !isPaused ||
            saveSystem == null)
        {
            return;
        }

        // Preserve your existing save-and-resume behavior.
        ApplyPauseState(false);

        lastStateChangeFrame = Time.frameCount;
        lastNavigationFrame = Time.frameCount;

        saveSystem.SaveGame();
    }

    public void QuitToMenu()
    {
        if (instance != this || !isActiveAndEnabled)
            return;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName) ||
            !Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError(
                $"Scene '{mainMenuSceneName}' cannot be loaded. " +
                "Check its name and add it to the build scene list.",
                this);

            return;
        }

        PrepareToQuit();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitToDesktop()
    {
        if (instance != this || !isActiveAndEnabled)
            return;

        PrepareToQuit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PrepareToQuit()
    {
        // Restore first so destruction cannot restore an old time scale
        // after preparing the scene transition.
        if (isPaused)
            ApplyPauseState(false);

        FlushPreferences();
        Time.timeScale = 1f;
    }
}