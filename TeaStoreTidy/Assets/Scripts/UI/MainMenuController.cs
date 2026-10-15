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

    private void Awake()
    {
        InputBindingPersistence.Apply(inputActions);
        Bind(newGameButton, StartNewGame);
        Bind(loadGameButton, LoadGame);
        Bind(settingsButton, () => ShowPanel(settingsPanel));
        Bind(keyBindingsButton, () => ShowPanel(keyBindingsPanel));
        Bind(backFromSettingsButton, () => ShowPanel(mainPanel));
        Bind(backFromBindingsButton, () => ShowPanel(settingsPanel));
        Bind(quitButton, Quit);
        Bind(keyboardBindingsTabButton, () => ShowBindingTab(false));
        Bind(controllerBindingsTabButton, () => ShowBindingTab(true));
        ConfigureSettings();
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
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MasterVolume", 1f));
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            SetMasterVolume(masterVolumeSlider.value);
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt("Quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1));
            qualityDropdown.RefreshShownValue();
            qualityDropdown.onValueChanged.AddListener(SetQuality);
            SetQuality(qualityDropdown.value);
        }

        if (resolutionDropdown != null)
        {
            availableResolutions.Clear();
            foreach (Resolution resolution in Screen.resolutions)
            {
                bool duplicate = availableResolutions.Exists(r => r.width == resolution.width && r.height == resolution.height);
                if (!duplicate) availableResolutions.Add(resolution);
            }
            resolutionDropdown.ClearOptions();
            var labels = new List<string>();
            foreach (Resolution resolution in availableResolutions)
                labels.Add($"{resolution.width} x {resolution.height}");
            resolutionDropdown.AddOptions(labels);
            int selected = availableResolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
            if (selected < 0) selected = Mathf.Max(0, availableResolutions.Count - 1);
            resolutionDropdown.SetValueWithoutNotify(selected);
            resolutionDropdown.RefreshShownValue();
            resolutionDropdown.onValueChanged.AddListener(SetResolution);
            if (PlayerPrefs.HasKey("Resolution"))
                SetResolution(Mathf.Clamp(PlayerPrefs.GetInt("Resolution"), 0, Mathf.Max(0, availableResolutions.Count - 1)));
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) != 0);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            SetFullscreen(fullscreenToggle.isOn);
        }
    }

    private void SetMasterVolume(float value)
    {
        PlayerPrefs.SetFloat("MasterVolume", value);
        if (audioMixer != null && !string.IsNullOrWhiteSpace(masterVolumeParameter))
            audioMixer.SetFloat(masterVolumeParameter, value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f);
        else
            AudioListener.volume = value;
        PlayerPrefs.Save();
    }

    private void SetQuality(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
        PlayerPrefs.SetInt("Quality", index);
        PlayerPrefs.Save();
    }

    private void SetResolution(int index)
    {
        if (index < 0 || index >= availableResolutions.Count) return;
        Resolution selected = availableResolutions[index];
        Screen.SetResolution(selected.width, selected.height, Screen.fullScreen);
        PlayerPrefs.SetInt("Resolution", index);
        PlayerPrefs.Save();
    }

    private void SetFullscreen(bool enabled)
    {
        Screen.fullScreen = enabled;
        PlayerPrefs.SetInt("Fullscreen", enabled ? 1 : 0);
        PlayerPrefs.Save();
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
