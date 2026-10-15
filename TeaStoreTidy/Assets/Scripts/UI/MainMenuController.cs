using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Controls the Main Menu scene. Assign the UI references in the Inspector.</summary>
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string gameplaySceneName = "MainGame";
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject keyBindingsPanel;
    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button keyBindingsButton;
    [SerializeField] private Button backFromSettingsButton;
    [SerializeField] private Button backFromBindingsButton;
    [SerializeField] private Button applySettingsButton;
    [SerializeField] private Button quitButton;
    [Header("Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParameter = "MasterVolume";
    [Header("Rebinding")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform bindingRowsParent;
    [SerializeField] private Transform controllerBindingRowsParent;
    [SerializeField] private Button keyboardBindingsTabButton;
    [SerializeField] private Button controllerBindingsTabButton;
    [SerializeField] private GameObject keyboardBindingsContainer;
    [SerializeField] private GameObject controllerBindingsContainer;
    [SerializeField] private RebindRow rebindRowPrefab;
    [SerializeField] private TMP_Text statusText;

    private readonly List<Resolution> availableResolutions = new List<Resolution>();
    private float pendingVolume;
    private int pendingQuality;
    private int pendingResolution;
    private bool pendingFullscreen;

    private void Awake()
    {
        InputBindingPersistence.Apply(inputActions);
        ConfigureSettings();
        CreateSettingsArrowControls();
        Bind(newGameButton, StartNewGame);
        Bind(loadGameButton, LoadGame);
        Bind(settingsButton, OpenSettings);
        Bind(keyBindingsButton, () => ShowPanel(keyBindingsPanel));
        Bind(backFromSettingsButton, CancelSettings);
        Bind(backFromBindingsButton, () => ShowPanel(settingsPanel));
        Bind(applySettingsButton, ApplySettings);
        Bind(quitButton, Quit);
        Bind(keyboardBindingsTabButton, () => ShowBindingTab(false));
        Bind(controllerBindingsTabButton, () => ShowBindingTab(true));
        BuildRebindRows();
        ShowBindingTab(false);
        ShowPanel(mainPanel);
        if (loadGameButton != null)
            loadGameButton.interactable = GameSaveSystem.SaveExists;
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void StartNewGame()
    {
        GameSaveSystem.DeleteSave();
        GameSaveSystem.RequestLoadOnNextGameScene(false);
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void LoadGame()
    {
        if (!GameSaveSystem.SaveExists)
        {
            SetStatus("No saved game found.");
            return;
        }

        GameSaveSystem.RequestLoadOnNextGameScene(true);
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void ShowPanel(GameObject panel)
    {
        if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
        if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
        if (keyBindingsPanel != null) keyBindingsPanel.SetActive(panel == keyBindingsPanel);
    }

    private void ConfigureSettings()
    {
        availableResolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            bool duplicate = availableResolutions.Exists(r => r.width == resolution.width && r.height == resolution.height);
            if (!duplicate) availableResolutions.Add(resolution);
        }

        pendingVolume = PlayerPrefs.GetFloat("MasterVolume", AudioListener.volume);
        pendingQuality = Mathf.Clamp(PlayerPrefs.GetInt("Quality", QualitySettings.GetQualityLevel()), 0,
            Mathf.Max(0, QualitySettings.names.Length - 1));
        pendingFullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) != 0;

        if (QualitySettings.names.Length > 0)
            QualitySettings.SetQualityLevel(pendingQuality, true);
        AudioListener.volume = pendingVolume;
        if (audioMixer != null && !string.IsNullOrWhiteSpace(masterVolumeParameter))
            audioMixer.SetFloat(masterVolumeParameter, pendingVolume <= 0.0001f ? -80f : Mathf.Log10(pendingVolume) * 20f);
        Screen.fullScreen = pendingFullscreen;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(pendingVolume);
            masterVolumeSlider.onValueChanged.AddListener(value => pendingVolume = value);
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.SetValueWithoutNotify(pendingQuality);
            qualityDropdown.RefreshShownValue();
            qualityDropdown.onValueChanged.AddListener(value => pendingQuality = value);
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            var labels = new List<string>();
            foreach (Resolution resolution in availableResolutions)
                labels.Add($"{resolution.width} x {resolution.height}");
            resolutionDropdown.AddOptions(labels);
            pendingResolution = availableResolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
            if (PlayerPrefs.HasKey("Resolution"))
                pendingResolution = Mathf.Clamp(PlayerPrefs.GetInt("Resolution"), 0, Mathf.Max(0, availableResolutions.Count - 1));
            if (pendingResolution < 0) pendingResolution = Mathf.Max(0, availableResolutions.Count - 1);
            resolutionDropdown.SetValueWithoutNotify(pendingResolution);
            resolutionDropdown.RefreshShownValue();
            resolutionDropdown.onValueChanged.AddListener(value => pendingResolution = value);
            resolutionDropdown.enabled = false;
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(pendingFullscreen);
            fullscreenToggle.onValueChanged.AddListener(value => pendingFullscreen = value);
        }
        if (PlayerPrefs.HasKey("Resolution") && pendingResolution >= 0 && pendingResolution < availableResolutions.Count)
        {
            Resolution saved = availableResolutions[pendingResolution];
            Screen.SetResolution(saved.width, saved.height, pendingFullscreen);
        }
    }

    private void CreateSettingsArrowControls()
    {
        if (settingsPanel == null || backFromSettingsButton == null) return;
        CreateArrowPair(qualityDropdown, () => CycleQuality(-1), () => CycleQuality(1), "Quality");
        CreateArrowPair(resolutionDropdown, () => CycleResolution(-1), () => CycleResolution(1), "Resolution");

        RectTransform backRect = backFromSettingsButton.GetComponent<RectTransform>();
        if (backRect != null) backRect.anchoredPosition = new Vector2(backRect.anchoredPosition.x, -288f);
        if (applySettingsButton == null)
            applySettingsButton = CloneStyledButton(backFromSettingsButton, "Apply Settings", "Apply Settings", new Vector2(0, -218), new Vector2(420, 58));
    }

    private void CreateArrowPair(TMP_Dropdown dropdown, UnityEngine.Events.UnityAction previous,
        UnityEngine.Events.UnityAction next, string controlName)
    {
        if (dropdown == null || backFromSettingsButton == null) return;
        RectTransform valueRect = dropdown.GetComponent<RectTransform>();
        Vector2 center = valueRect.anchoredPosition;
        valueRect.sizeDelta = new Vector2(136, valueRect.sizeDelta.y);
        dropdown.enabled = false;
        // Plain ASCII chevrons render with the project's configured TMP font.
        CloneStyledButton(backFromSettingsButton, controlName + " Previous", "<", center + new Vector2(-98, 0), new Vector2(44, 42)).onClick.AddListener(previous);
        CloneStyledButton(backFromSettingsButton, controlName + " Next", ">", center + new Vector2(98, 0), new Vector2(44, 42)).onClick.AddListener(next);
    }

    private Button CloneStyledButton(Button template, string objectName, string label, Vector2 position, Vector2 size)
    {
        GameObject clone = Instantiate(template.gameObject, settingsPanel.transform, false);
        clone.name = objectName;
        RectTransform rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Button button = clone.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
        return button;
    }

    private void CycleQuality(int direction)
    {
        if (QualitySettings.names.Length == 0 || qualityDropdown == null) return;
        pendingQuality = (pendingQuality + direction + QualitySettings.names.Length) % QualitySettings.names.Length;
        qualityDropdown.SetValueWithoutNotify(pendingQuality);
        qualityDropdown.RefreshShownValue();
    }

    private void CycleResolution(int direction)
    {
        if (availableResolutions.Count == 0 || resolutionDropdown == null) return;
        pendingResolution = (pendingResolution + direction + availableResolutions.Count) % availableResolutions.Count;
        resolutionDropdown.SetValueWithoutNotify(pendingResolution);
        resolutionDropdown.RefreshShownValue();
    }

    private void OpenSettings()
    {
        ResetPendingSettings();
        ShowPanel(settingsPanel);
    }

    private void CancelSettings()
    {
        ResetPendingSettings();
        ShowPanel(mainPanel);
    }

    private void ResetPendingSettings()
    {
        pendingVolume = AudioListener.volume;
        pendingQuality = QualitySettings.GetQualityLevel();
        pendingFullscreen = Screen.fullScreen;
        int currentResolution = availableResolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
        if (currentResolution >= 0) pendingResolution = currentResolution;
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(pendingVolume);
        if (qualityDropdown != null) { qualityDropdown.SetValueWithoutNotify(pendingQuality); qualityDropdown.RefreshShownValue(); }
        if (resolutionDropdown != null) { resolutionDropdown.SetValueWithoutNotify(pendingResolution); resolutionDropdown.RefreshShownValue(); }
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(pendingFullscreen);
    }

    private void ApplySettings()
    {
        AudioListener.volume = pendingVolume;
        if (audioMixer != null && !string.IsNullOrWhiteSpace(masterVolumeParameter))
            audioMixer.SetFloat(masterVolumeParameter, pendingVolume <= 0.0001f ? -80f : Mathf.Log10(pendingVolume) * 20f);
        if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel(pendingQuality, true);
        if (pendingResolution >= 0 && pendingResolution < availableResolutions.Count)
        {
            Resolution selected = availableResolutions[pendingResolution];
            Screen.SetResolution(selected.width, selected.height, pendingFullscreen);
        }
        else Screen.fullScreen = pendingFullscreen;

        PlayerPrefs.SetFloat("MasterVolume", pendingVolume);
        PlayerPrefs.SetInt("Quality", pendingQuality);
        PlayerPrefs.SetInt("Resolution", pendingResolution);
        PlayerPrefs.SetInt("Fullscreen", pendingFullscreen ? 1 : 0);
        PlayerPrefs.Save();
        SetStatus("Settings applied.");
    }

    private void BuildRebindRows()
    {
        if (inputActions == null || bindingRowsParent == null || rebindRowPrefab == null) return;
        InputActionMap map = inputActions.FindActionMap("Player", false);
        if (map == null) return;

        // Keep keyboard/mouse and gamepad controls in distinct, user-selectable tabs.
        string[] actionNames = { "Move", "Look", "Interact", "Drop", "CycleItems", "Jump", "Sprint", "ScentTrail" };
        foreach (string actionName in actionNames)
        {
            InputAction action = map.FindAction(actionName, false);
            if (action == null) continue;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite) continue;
                bool keyboardBinding = IsKeyboardOrMouseBinding(binding);
                bool gamepadBinding = IsGamepadBinding(binding);
                if (keyboardBinding && IsDuplicateMoveArrowBinding(actionName, binding))
                    continue;
                if (keyboardBinding && bindingRowsParent != null && !(binding.path.Contains("<Pointer>/delta") || binding.path == "<Mouse>/delta"))
                    CreateBindingRow(bindingRowsParent, action, i, binding, actionName);
                else if (gamepadBinding && controllerBindingRowsParent != null)
                    CreateBindingRow(controllerBindingRowsParent, action, i, binding, actionName);
            }
        }
    }

    private void CreateBindingRow(Transform parent, InputAction action, int index, InputBinding binding, string actionName)
    {
        RebindRow row = Instantiate(rebindRowPrefab, parent);
        string label = GetBindingLabel(actionName, binding);
        row.Initialize(inputActions, action, index, label, SetStatus);
    }

    private static bool IsDuplicateMoveArrowBinding(string actionName, InputBinding binding)
    {
        if (actionName != "Move" || !binding.isPartOfComposite)
            return false;

        return binding.path == "<Keyboard>/upArrow" ||
               binding.path == "<Keyboard>/downArrow" ||
               binding.path == "<Keyboard>/leftArrow" ||
               binding.path == "<Keyboard>/rightArrow";
    }

    private static string GetBindingLabel(string actionName, InputBinding binding)
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
        return binding.groups.Contains("Keyboard&Mouse") ||
               binding.path.StartsWith("<Keyboard>") || binding.path.StartsWith("<Mouse>");
    }

    private static bool IsGamepadBinding(InputBinding binding)
    {
        return binding.groups.Contains("Gamepad") || binding.path.StartsWith("<Gamepad>");
    }

    private void ShowBindingTab(bool showController)
    {
        if (keyboardBindingsContainer != null) keyboardBindingsContainer.SetActive(!showController);
        if (controllerBindingsContainer != null) controllerBindingsContainer.SetActive(showController);
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
