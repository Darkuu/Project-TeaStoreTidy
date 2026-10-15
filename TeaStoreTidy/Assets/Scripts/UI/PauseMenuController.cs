using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Displays an in-game menu without stopping time or disabling player movement.</summary>
public sealed class PauseMenuController : MonoBehaviour
{
    private GameSaveSystem saveSystem;
    private PauseMenuView view;
    private GameObject root;
    private GameObject pausePanel;
    private GameObject settingsPanel;
    private GameObject quitPanel;
    private TMP_Text statusText;
    private TMP_Text settingsStatusText;
    private TMP_Text qualityValue;
    private TMP_Text resolutionValue;
    private TMP_Text fullscreenValue;
    private Slider volumeSlider;
    private readonly List<Resolution> resolutions = new List<Resolution>();
    private int pendingQuality;
    private int pendingResolution;
    private float pendingVolume;
    private bool pendingFullscreen;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        saveSystem = GetComponent<GameSaveSystem>();
        CreateMenu();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (isOpen) Close();
        else Open();
    }

    private void Open()
    {
        isOpen = true;
        pausePanel.SetActive(true);
        settingsPanel.SetActive(false);
        quitPanel.SetActive(false);
        root.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshSettingLabels();
        SetStatus(string.Empty);
    }

    private void Close()
    {
        isOpen = false;
        root.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void CreateMenu()
    {
        GameObject prefab = Resources.Load<GameObject>("PauseMenuCanvas");
        if (prefab == null)
        {
            Debug.LogError("PauseMenuCanvas prefab was not found. Place it at Assets/Resources/PauseMenuCanvas.prefab.", this);
            enabled = false;
            return;
        }

        view = Instantiate(prefab).GetComponent<PauseMenuView>();
        if (view == null)
        {
            Debug.LogError("PauseMenuCanvas prefab needs a PauseMenuView component.", this);
            enabled = false;
            return;
        }
        root = view.gameObject;
        pausePanel = view.pausePanel;
        settingsPanel = view.settingsPanel;
        quitPanel = view.quitPanel;
        statusText = view.statusText;
        settingsStatusText = view.settingsStatusText;
        qualityValue = view.qualityValue;
        resolutionValue = view.resolutionValue;
        fullscreenValue = view.fullscreenValue;
        volumeSlider = view.volumeSlider;

        resolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            if (!resolutions.Exists(r => r.width == resolution.width && r.height == resolution.height))
                resolutions.Add(resolution);
        }
        pendingResolution = resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
        if (pendingResolution < 0) pendingResolution = Mathf.Max(0, resolutions.Count - 1);
        if (PlayerPrefs.HasKey("Resolution"))
            pendingResolution = Mathf.Clamp(PlayerPrefs.GetInt("Resolution"), 0, Mathf.Max(0, resolutions.Count - 1));

        pendingVolume = PlayerPrefs.GetFloat("MasterVolume", AudioListener.volume);
        pendingQuality = Mathf.Clamp(PlayerPrefs.GetInt("Quality", QualitySettings.GetQualityLevel()), 0,
            Mathf.Max(0, QualitySettings.names.Length - 1));
        pendingFullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) != 0;
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(pendingVolume);
            volumeSlider.onValueChanged.AddListener(value => pendingVolume = value);
        }

        Bind(view.resumeButton, Resume);
        Bind(view.settingsButton, ShowSettings);
        Bind(view.saveButton, SaveGame);
        Bind(view.loadButton, LoadGame);
        Bind(view.quitButton, ShowQuitOptions);
        Bind(view.quitToMenuButton, QuitToMenu);
        Bind(view.quitToDesktopButton, QuitToDesktop);
        Bind(view.quitBackButton, ShowPause);
        Bind(view.applySettingsButton, ApplySettings);
        Bind(view.settingsBackButton, ShowPause);
        Bind(view.previousQualityButton, PreviousQuality);
        Bind(view.nextQualityButton, NextQuality);
        Bind(view.previousResolutionButton, PreviousResolution);
        Bind(view.nextResolutionButton, NextResolution);
        Bind(view.previousFullscreenButton, ToggleFullscreen);
        Bind(view.nextFullscreenButton, ToggleFullscreen);

        root.SetActive(false);
        RefreshSettingLabels();
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void Resume() => Close();

    private void ShowSettings()
    {
        ResetPendingSettings();
        pausePanel.SetActive(false);
        quitPanel.SetActive(false);
        settingsPanel.SetActive(true);
        settingsStatusText.text = string.Empty;
        RefreshSettingLabels();
    }

    private void ShowPause()
    {
        ResetPendingSettings();
        settingsPanel.SetActive(false);
        quitPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    private void ShowQuitOptions()
    {
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        quitPanel.SetActive(true);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void SaveGame()
    {
        if (saveSystem == null) saveSystem = GetComponent<GameSaveSystem>();
        if (saveSystem == null) { SetStatus("Save system is unavailable."); return; }
        saveSystem.SaveGame();
        SetStatus("Game saved.");
    }

    private void LoadGame()
    {
        if (saveSystem == null) saveSystem = GetComponent<GameSaveSystem>();
        if (saveSystem == null || !GameSaveSystem.SaveExists) { SetStatus("No saved game found."); return; }
        saveSystem.LoadGame();
        SetStatus("Game loaded.");
    }

    private void QuitToMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }

    private void QuitToDesktop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PreviousQuality() => ChangeQuality(-1);
    private void NextQuality() => ChangeQuality(1);

    private void ChangeQuality(int direction)
    {
        string[] names = QualitySettings.names;
        if (names.Length == 0) return;
        pendingQuality = (pendingQuality + direction + names.Length) % names.Length;
        RefreshSettingLabels();
    }

    private void PreviousResolution() => ChangeResolution(-1);
    private void NextResolution() => ChangeResolution(1);

    private void ChangeResolution(int direction)
    {
        if (resolutions.Count == 0) return;
        pendingResolution = (pendingResolution + direction + resolutions.Count) % resolutions.Count;
        RefreshSettingLabels();
    }

    private void ToggleFullscreen()
    {
        pendingFullscreen = !pendingFullscreen;
        RefreshSettingLabels();
    }

    private void RefreshSettingLabels()
    {
        if (qualityValue != null && QualitySettings.names.Length > 0)
            qualityValue.text = QualitySettings.names[Mathf.Clamp(pendingQuality, 0, QualitySettings.names.Length - 1)];

        if (resolutionValue != null && resolutions.Count > 0)
        {
            Resolution selected = resolutions[Mathf.Clamp(pendingResolution, 0, resolutions.Count - 1)];
            resolutionValue.text = $"{selected.width} x {selected.height}";
        }

        if (fullscreenValue != null)
            fullscreenValue.text = pendingFullscreen ? "On" : "Off";
    }

    private void ResetPendingSettings()
    {
        pendingVolume = AudioListener.volume;
        pendingQuality = QualitySettings.GetQualityLevel();
        pendingFullscreen = Screen.fullScreen;
        int currentResolution = resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
        if (currentResolution >= 0) pendingResolution = currentResolution;
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(pendingVolume);
        RefreshSettingLabels();
    }

    private void ApplySettings()
    {
        AudioListener.volume = pendingVolume;
        if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel(pendingQuality, true);
        if (pendingResolution >= 0 && pendingResolution < resolutions.Count)
        {
            Resolution selected = resolutions[pendingResolution];
            Screen.SetResolution(selected.width, selected.height, pendingFullscreen);
        }
        else Screen.fullScreen = pendingFullscreen;

        PlayerPrefs.SetFloat("MasterVolume", pendingVolume);
        PlayerPrefs.SetInt("Quality", pendingQuality);
        PlayerPrefs.SetInt("Resolution", pendingResolution);
        PlayerPrefs.SetInt("Fullscreen", pendingFullscreen ? 1 : 0);
        PlayerPrefs.Save();
        settingsStatusText.text = "Settings applied.";
    }

}
