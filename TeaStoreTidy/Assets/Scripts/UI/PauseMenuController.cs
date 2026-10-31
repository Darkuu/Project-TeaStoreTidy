using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    private static PauseMenuController instance;

    [Header("Menu")]
    [SerializeField] private GameObject menuRoot;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button quitToMenuButton;
    [SerializeField] private Button quitToDesktopButton;

    [Header("References — Auto-found in parents if empty")]
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameSaveSystem saveSystem;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    private bool isPaused;
    private int lastStateChangeFrame = -1;

    private float previousTimeScale;
    private bool playerWasEnabled;
    private bool inventoryWasEnabled;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public bool IsPaused => isPaused;

    // Also resets the singleton when domain reload is disabled.
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

        // Hiding the menu must not disable the script listening for Escape.
        if (transform.IsChildOf(menuRoot.transform))
        {
            Debug.LogError(
                "PauseMenuController must be outside Menu Root. " +
                "Put it on an always-active parent or separate object.",
                this
            );

            enabled = false;
            return;
        }

        if (instance != null && instance != this)
        {
            Debug.LogWarning(
                "Duplicate PauseMenuController detected. " +
                "Only one pause controller may handle Escape.",
                this
            );

            menuRoot.SetActive(false);
            enabled = false;
            return;
        }

        instance = this;

        if (playerController == null)
            playerController =
                GetComponentInParent<FirstPersonController>();

        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();

        if (saveSystem == null)
            saveSystem = GetComponentInParent<GameSaveSystem>();

        Bind(resumeButton, Resume);
        Bind(saveGameButton, SaveGame);
        Bind(quitToMenuButton, QuitToMenu);
        Bind(quitToDesktopButton, QuitToDesktop);

        if (saveGameButton != null)
            saveGameButton.interactable = saveSystem != null;

        menuRoot.SetActive(false);
    }

    private void Update()
    {
        if (instance != this)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetPaused(!isPaused);
        }
    }

    private void OnDisable()
    {
        // Do not leave the game frozen if this controller is disabled.
        if (instance == this && isPaused)
            ApplyPauseState(false);
    }

    private void OnDestroy()
    {
        Unbind(resumeButton, Resume);
        Unbind(saveGameButton, SaveGame);
        Unbind(quitToMenuButton, QuitToMenu);
        Unbind(quitToDesktopButton, QuitToDesktop);

        if (instance != this)
            return;

        if (isPaused)
            ApplyPauseState(false);

        instance = null;
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button == null)
            return;

        // Remove only this callback, not unrelated button listeners.
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

        // Prevent Escape and UI callbacks from toggling twice in one frame.
        if (lastStateChangeFrame == Time.frameCount)
            return;

        lastStateChangeFrame = Time.frameCount;
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

            menuRoot.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            if (menuRoot != null)
                menuRoot.SetActive(false);

            Time.timeScale = previousTimeScale;

            if (playerController != null)
                playerController.enabled = playerWasEnabled;

            if (inventory != null)
                inventory.enabled = inventoryWasEnabled;

            // Restore after enabling the player, since its OnEnable()
            // also changes the cursor.
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void SaveGame()
    {
        if (!isPaused || saveSystem == null)
            return;

        saveSystem.SaveGame();
    }

    public void QuitToMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName) ||
            !Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError(
                $"Scene '{mainMenuSceneName}' cannot be loaded. " +
                "Check its name and add it to the build scene list.",
                this
            );

            return;
        }

        PrepareToQuit();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitToDesktop()
    {
        PrepareToQuit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PrepareToQuit()
    {
        // Restore first so OnDisable/OnDestroy cannot restore an old
        // time scale after we have prepared the scene transition.
        if (isPaused)
            ApplyPauseState(false);

        Time.timeScale = 1f;
    }
}