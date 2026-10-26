using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Controls the editable pause menu prefab.</summary>
public sealed class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button quitToMenuButton;
    [SerializeField] private Button quitToDesktopButton;

    private bool isPaused;
    private FirstPersonController playerController;
    private PlayerInventory inventory;
    private GameSaveSystem saveSystem;

    private void Awake()
    {
        playerController = GetComponentInParent<FirstPersonController>();
        inventory = GetComponentInParent<PlayerInventory>();
        saveSystem = GetComponentInParent<GameSaveSystem>();

        if (menuRoot == null)
        {
            Debug.LogError("Assign the pause menu prefab's Menu Root.", this);
            enabled = false;
            return;
        }

        Bind(resumeButton, Resume);
        Bind(saveGameButton, SaveGame);
        Bind(quitToMenuButton, QuitToMenu);
        Bind(quitToDesktopButton, QuitToDesktop);
        menuRoot.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetPaused(!isPaused);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void SetPaused(bool paused)
    {
        isPaused = paused;
        menuRoot.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
        if (playerController != null) playerController.enabled = !paused;
        if (inventory != null) inventory.enabled = !paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    private void Resume() => SetPaused(false);

    private void SaveGame()
    {
        if (saveSystem != null)
            saveSystem.SaveGame();
    }

    private void QuitToMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("Main Menu");
    }

    private void QuitToDesktop()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
