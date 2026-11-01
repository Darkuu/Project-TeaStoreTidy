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
    private const string ResolutionWidthKey = "ResolutionWidth";
    private const string ResolutionHeightKey = "ResolutionHeight";

    [Header("Menu")]
    [SerializeField] private GameObject menuRoot;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button quitToMenuButton;
    [SerializeField] private Button quitToDesktopButton;

    [Header("Settings Panel")]
    [Tooltip("Assign Settings Root. It should be separate from Menu Root.")]
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

    [Header("Keybindings Panel")]
    [SerializeField] private GameObject keyBindingsPanel;
    [SerializeField] private Button keyBindingsButton;
    [SerializeField] private Button backFromBindingsButton;

    [Header("Rebinding")]
    [SerializeField] private InputActionAsset inputActions;

    [Tooltip("Optional: assign the player's PlayerInput so rebinding " +
             "uses its runtime action asset.")]
    [SerializeField] private PlayerInput playerInput;

    [SerializeField] private Transform bindingRowsParent;
    [SerializeField] private Transform controllerBindingRowsParent;
    [SerializeField] private RebindRow rebindRowPrefab;
    [SerializeField] private TMP_Text statusText;

    [Header("Binding Tabs")]
    [SerializeField] private Button keyboardBindingsTabButton;
    [SerializeField] private Button controllerBindingsTabButton;
    [SerializeField] private GameObject keyboardBindingsContainer;
    [SerializeField] private GameObject controllerBindingsContainer;

    [Header("References — Auto-found in parents if empty")]
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameSaveSystem saveSystem;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    private readonly List<Vector2Int> availableResolutions =
        new List<Vector2Int>();

    private readonly List<Button> rebindButtons = new List<Button>();

    private bool isPaused;
    private bool settingsOpen;
    private bool bindingsOpen;
    private bool settingsInitialized;
    private bool preferencesDirty;
    private bool settingsPanelsValid;
    private bool bindingsPanelValid;

    // RebindRow remains unchanged. The controller tracks its button
    // and completion callback to prevent navigation during rebinding.
    private bool rebindInProgress;
    private int rebindFinishedFrame = -1;

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

    private bool NavigationBlocked =>
        rebindInProgress ||
        rebindFinishedFrame == Time.frameCount;

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

        if (transform.IsChildOf(menuRoot.transform))
        {
            Debug.LogError(
                "PauseMenuController must be outside Menu Root. " +
                "Put it on the always-active Pause Menu parent.",
                this);

            enabled = false;
            return;
        }

        if (instance != null && instance != this)
        {
            Debug.LogWarning(
                "Duplicate PauseMenuController detected.",
                this);

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

        if (playerInput == null)
            playerInput = GetComponentInParent<PlayerInput>();

        Bind(resumeButton, Resume);
        Bind(saveGameButton, SaveGame);
        Bind(quitToMenuButton, QuitToMenu);
        Bind(quitToDesktopButton, QuitToDesktop);

        Bind(settingsButton, OpenSettings);
        Bind(backFromSettingsButton, CloseSettings);

        Bind(keyBindingsButton, OpenKeyBindings);
        Bind(backFromBindingsButton, CloseKeyBindings);
        Bind(keyboardBindingsTabButton, ShowKeyboardTab);
        Bind(controllerBindingsTabButton, ShowControllerTab);

        settingsPanelsValid = ValidateSettingsPanel();
        bindingsPanelValid = ValidateBindingsPanel();

        if (settingsButton != null)
            settingsButton.interactable = settingsPanelsValid;

        if (keyBindingsButton != null)
            keyBindingsButton.interactable =
                settingsPanelsValid && bindingsPanelValid;

        if (saveGameButton != null)
            saveGameButton.interactable = saveSystem != null;

        if (settingsPanelsValid)
            settingsPanel.SetActive(false);

        if (bindingsPanelValid)
            keyBindingsPanel.SetActive(false);

        menuRoot.SetActive(false);
    }

    private void Start()
    {
        if (instance != this)
            return;

        // Mixer changes are initialized in Start, not Awake.
        InitializeSettings();

        if (inputActions == null && inventory != null)
            inputActions = inventory.InputActions;

        // PlayerInput may use a runtime copy of the action asset.
        if (playerInput != null && playerInput.actions != null)
            inputActions = playerInput.actions;

        InputBindingPersistence.Apply(inputActions);

        BuildRebindRows();
        ShowBindingTab(false);
    }

    private bool ValidateSettingsPanel()
    {
        if (settingsPanel == null)
            return false;

        if (!AreSeparatePanels(menuRoot, settingsPanel) ||
            transform.IsChildOf(settingsPanel.transform))
        {
            Debug.LogError(
                "Menu Root and Settings Root must be separate panels. " +
                "The controller must remain outside both.",
                this);

            return false;
        }

        return true;
    }

    private bool ValidateBindingsPanel()
    {
        if (keyBindingsPanel == null)
            return false;

        if (!AreSeparatePanels(menuRoot, keyBindingsPanel) ||
            (settingsPanel != null &&
             !AreSeparatePanels(settingsPanel, keyBindingsPanel)) ||
            transform.IsChildOf(keyBindingsPanel.transform))
        {
            Debug.LogError(
                "Key Bindings Root must be separate from Menu Root " +
                "and Settings Root. Keep the controller outside all panels.",
                this);

            return false;
        }

        return true;
    }

    private static bool AreSeparatePanels(GameObject first, GameObject second)
    {
        return first != second &&
               !first.transform.IsChildOf(second.transform) &&
               !second.transform.IsChildOf(first.transform);
    }

    private void Update()
    {
        if (instance != this || NavigationBlocked)
            return;

        if (Keyboard.current == null ||
            !Keyboard.current.escapeKey.wasPressedThisFrame ||
            lastNavigationFrame == Time.frameCount)
        {
            return;
        }

        if (isPaused && bindingsOpen)
        {
            CloseKeyBindings();
        }
        else if (isPaused && settingsOpen)
        {
            CloseSettings();
        }
        else
        {
            SetPaused(!isPaused);
        }
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

        Unbind(keyBindingsButton, OpenKeyBindings);
        Unbind(backFromBindingsButton, CloseKeyBindings);
        Unbind(keyboardBindingsTabButton, ShowKeyboardTab);
        Unbind(controllerBindingsTabButton, ShowControllerTab);

        foreach (Button button in rebindButtons)
            Unbind(button, OnRebindButtonClicked);

        if (settingsInitialized)
        {
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.RemoveListener(
                    SetMasterVolume);

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
        if (instance != this ||
            !isActiveAndEnabled ||
            NavigationBlocked ||
            isPaused == paused ||
            lastStateChangeFrame == Time.frameCount)
        {
            return;
        }

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

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            ShowPausePanel();
            FlushPreferences();

            Time.timeScale = previousTimeScale;

            if (playerController != null)
                playerController.enabled = playerWasEnabled;

            if (inventory != null)
                inventory.enabled = inventoryWasEnabled;

            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
    }

    private void ShowPausePanel()
    {
        settingsOpen = false;
        bindingsOpen = false;

        if (settingsPanelsValid && settingsPanel != null)
            settingsPanel.SetActive(false);

        if (bindingsPanelValid && keyBindingsPanel != null)
            keyBindingsPanel.SetActive(false);

        if (menuRoot != null)
            menuRoot.SetActive(isPaused);
    }

    public void OpenSettings()
    {
        if (instance != this ||
            !isActiveAndEnabled ||
            !isPaused ||
            !settingsPanelsValid ||
            NavigationBlocked)
        {
            return;
        }

        InitializeSettings();
        RefreshSettingsControls();

        settingsOpen = true;
        bindingsOpen = false;
        lastNavigationFrame = Time.frameCount;

        menuRoot.SetActive(false);

        if (bindingsPanelValid)
            keyBindingsPanel.SetActive(false);

        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (instance != this ||
            !isPaused ||
            !settingsOpen ||
            NavigationBlocked)
        {
            return;
        }

        lastNavigationFrame = Time.frameCount;
        ShowPausePanel();
        FlushPreferences();
    }

    public void OpenKeyBindings()
    {
        if (instance != this ||
            !isActiveAndEnabled ||
            !isPaused ||
            !settingsPanelsValid ||
            !bindingsPanelValid ||
            NavigationBlocked)
        {
            return;
        }

        settingsOpen = false;
        bindingsOpen = true;
        lastNavigationFrame = Time.frameCount;

        menuRoot.SetActive(false);
        settingsPanel.SetActive(false);
        keyBindingsPanel.SetActive(true);

        ShowBindingTab(false);
        SetStatus(string.Empty);
    }

    public void CloseKeyBindings()
    {
        if (instance != this ||
            !isPaused ||
            !bindingsOpen ||
            NavigationBlocked)
        {
            return;
        }

        OpenSettings();
        FlushPreferences();
    }

    private void ShowKeyboardTab()
    {
        if (!NavigationBlocked)
            ShowBindingTab(false);
    }

    private void ShowControllerTab()
    {
        if (!NavigationBlocked)
            ShowBindingTab(true);
    }

    private void ShowBindingTab(bool showController)
    {
        if (keyboardBindingsContainer != null)
            keyboardBindingsContainer.SetActive(!showController);

        if (controllerBindingsContainer != null)
            controllerBindingsContainer.SetActive(showController);
    }

    private void BuildRebindRows()
    {
        if (!bindingsPanelValid ||
            inputActions == null ||
            rebindRowPrefab == null)
        {
            return;
        }

        InputActionMap map = inputActions.FindActionMap("Player", false);

        if (map == null)
        {
            Debug.LogWarning(
                "The input action asset has no 'Player' action map.",
                this);

            return;
        }

        string[] actionNames =
        {
            "Move", "Look", "Interact", "Drop",
            "CycleItems", "Jump", "Sprint", "ScentTrail"
        };

        foreach (string actionName in actionNames)
        {
            InputAction action = map.FindAction(actionName, false);

            if (action == null)
                continue;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];

                if (binding.isComposite)
                    continue;

                string path = binding.path ?? string.Empty;

                if (IsKeyboardOrMouseBinding(binding))
                {
                    if (IsDuplicateMoveArrowBinding(actionName, binding))
                        continue;

                    if (path == "<Pointer>/delta" ||
                        path == "<Mouse>/delta")
                    {
                        continue;
                    }

                    CreateBindingRow(
                        bindingRowsParent, action, i, binding, actionName);
                }
                else if (IsGamepadBinding(binding))
                {
                    CreateBindingRow(
                        controllerBindingRowsParent,
                        action, i, binding, actionName);
                }
            }
        }
    }

    private void CreateBindingRow(
        Transform parent,
        InputAction action,
        int index,
        InputBinding binding,
        string actionName)
    {
        if (parent == null)
            return;

        RebindRow row = Instantiate(rebindRowPrefab, parent);

        row.Initialize(
            inputActions,
            action,
            index,
            GetBindingLabel(actionName, binding),
            OnRebindReported);

        // Your supplied row has one button. Add our listener AFTER
        // Initialize so RebindRow begins its operation first.
        Button button = row.GetComponentInChildren<Button>(true);

        if (button == null)
        {
            Debug.LogWarning(
                "The RebindRow prefab needs a Rebind button.",
                row);

            return;
        }

        rebindButtons.Add(button);
        button.onClick.AddListener(OnRebindButtonClicked);
    }

    private void OnRebindButtonClicked()
    {
        // RebindRow disables its own button when rebinding begins.
        bool started = false;

        foreach (Button button in rebindButtons)
        {
            if (button != null && !button.interactable)
            {
                started = true;
                break;
            }
        }

        if (!started)
            return;

        rebindInProgress = true;

        // Prevent a second row from starting another operation.
        foreach (Button button in rebindButtons)
        {
            if (button != null)
                button.interactable = false;
        }

        SetStatus("Press a control. Press Escape to cancel.");
    }

    private void OnRebindReported(string message)
    {
        // These are the completion/cancellation callbacks supplied
        // by your existing RebindRow.
        if (this == null)
            return;

        rebindInProgress = false;
        rebindFinishedFrame = Time.frameCount;

        foreach (Button button in rebindButtons)
        {
            if (button != null)
                button.interactable = true;
        }

        SetStatus(message);
    }

    private static bool IsDuplicateMoveArrowBinding(
        string actionName,
        InputBinding binding)
    {
        if (actionName != "Move" || !binding.isPartOfComposite)
            return false;

        string path = binding.path ?? string.Empty;

        return path == "<Keyboard>/upArrow" ||
               path == "<Keyboard>/downArrow" ||
               path == "<Keyboard>/leftArrow" ||
               path == "<Keyboard>/rightArrow";
    }

    private static string GetBindingLabel(
        string actionName,
        InputBinding binding)
    {
        if (actionName == "Move" && binding.isPartOfComposite)
        {
            switch (binding.name)
            {
                case "up": return "Move Forward";
                case "down": return "Move Backward";
                case "left": return "Move Left";
                case "right": return "Move Right";
            }
        }

        switch (actionName)
        {
            case "Interact": return "Interact / Pick Up / Place";
            case "Drop": return "Drop Item";
            case "CycleItems": return "Change Held Item";
            case "ScentTrail": return "Scent Trail Ability";
            default: return actionName;
        }
    }

    private static bool IsKeyboardOrMouseBinding(InputBinding binding)
    {
        string groups = binding.groups ?? string.Empty;
        string path = binding.path ?? string.Empty;

        return groups.Contains("Keyboard&Mouse") ||
               path.StartsWith("<Keyboard>") ||
               path.StartsWith("<Mouse>");
    }

    private static bool IsGamepadBinding(InputBinding binding)
    {
        string groups = binding.groups ?? string.Empty;
        string path = binding.path ?? string.Empty;

        return groups.Contains("Gamepad") ||
               path.StartsWith("<Gamepad>");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
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
                    QualityKey, QualitySettings.GetQualityLevel()),
                0,
                QualitySettings.names.Length - 1);

            QualitySettings.SetQualityLevel(index, true);
        }

        desiredWidth = Screen.width;
        desiredHeight = Screen.height;

        desiredFullscreen = PlayerPrefs.GetInt(
            FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;

        bool hasDimensions =
            PlayerPrefs.HasKey(ResolutionWidthKey) &&
            PlayerPrefs.HasKey(ResolutionHeightKey);

        bool hasLegacyResolution = PlayerPrefs.HasKey("Resolution");

        if (hasDimensions)
        {
            Vector2Int savedSize = new Vector2Int(
                PlayerPrefs.GetInt(ResolutionWidthKey),
                PlayerPrefs.GetInt(ResolutionHeightKey));

            if (availableResolutions.Contains(savedSize))
            {
                desiredWidth = savedSize.x;
                desiredHeight = savedSize.y;
            }
        }
        else if (hasLegacyResolution && availableResolutions.Count > 0)
        {
            int index = Mathf.Clamp(
                PlayerPrefs.GetInt("Resolution"),
                0,
                availableResolutions.Count - 1);

            desiredWidth = availableResolutions[index].x;
            desiredHeight = availableResolutions[index].y;
        }

        if (hasDimensions ||
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

            resolutionDropdown.SetValueWithoutNotify(
                index < 0 ? 0 : index);

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
            float decibels = value <= 0.0001f
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

        // Retain compatibility with the unchanged main menu.
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
            saveSystem == null ||
            NavigationBlocked)
        {
            return;
        }

        // Preserve your original save-and-resume behavior.
        ApplyPauseState(false);

        lastStateChangeFrame = Time.frameCount;
        lastNavigationFrame = Time.frameCount;

        saveSystem.SaveGame();
    }

    public void QuitToMenu()
    {
        if (instance != this ||
            !isActiveAndEnabled ||
            NavigationBlocked)
        {
            return;
        }

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
        if (instance != this ||
            !isActiveAndEnabled ||
            NavigationBlocked)
        {
            return;
        }

        PrepareToQuit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PrepareToQuit()
    {
        if (isPaused)
            ApplyPauseState(false);

        FlushPreferences();
        Time.timeScale = 1f;
    }
}
