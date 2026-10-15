using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>References the editable visual elements in the pause menu prefab.</summary>
public sealed class PauseMenuView : MonoBehaviour
{
    public GameObject backdrop;
    public GameObject pausePanel;
    public GameObject settingsPanel;
    public GameObject quitPanel;

    public Button resumeButton;
    public Button settingsButton;
    public Button saveButton;
    public Button loadButton;
    public Button quitButton;
    public Button quitToMenuButton;
    public Button quitToDesktopButton;
    public Button quitBackButton;
    public Button applySettingsButton;
    public Button settingsBackButton;
    public Button previousQualityButton;
    public Button nextQualityButton;
    public Button previousResolutionButton;
    public Button nextResolutionButton;
    public Button previousFullscreenButton;
    public Button nextFullscreenButton;

    public Slider volumeSlider;
    public TMP_Text statusText;
    public TMP_Text settingsStatusText;
    public TMP_Text qualityValue;
    public TMP_Text resolutionValue;
    public TMP_Text fullscreenValue;
}
