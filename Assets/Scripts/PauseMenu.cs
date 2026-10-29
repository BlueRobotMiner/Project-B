// PauseMenu
// Escape toggles pause: freezes the game (timeScale 0), pauses audio, stops the player
// from moving or looking around, frees the mouse cursor, and shows the pause panel.
// Settings opens its own panel. Resume re-locks the cursor for first-person play.
// End Run returns to the menu scene and Quit exits the game (hidden on WebGL).
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused;

    public GameObject pausePanel;
    public GameObject settingsPanel;
    public Button resumeButton;
    public Button endRunButton;
    public Button settingsButton;
    public Button mainmenuButton;
    public string menuSceneName = "MainMenu";
    public KeyCode pauseKey = KeyCode.Escape;

    private FirstPersonController player;

    void Awake()
    {
        player = FindObjectOfType<FirstPersonController>();

        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (settingsButton != null) settingsButton.onClick.AddListener(() => OpenPanel(settingsPanel));
        if (endRunButton != null) endRunButton.onClick.AddListener(EndRun);
        if (mainmenuButton != null) mainmenuButton.onClick.AddListener(MainMenu);

        // Start unpaused without touching the player, so the wake-up sequence isn't overridden.
        IsPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // Escape toggles pause, unless the game over screen is up (no resuming a dead run).
    void Update()
    {
        if (GameOverMenu.IsGameOver || !Input.GetKeyDown(pauseKey)) return;
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        SetPaused(true);
    }

    public void Resume()
    {
        SetPaused(false);
    }

    // Central pause state: freezes time, audio, player input, and the cursor.
    void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        if (player != null) player.MovementEnabled = !paused;
        SetCursor(paused);
        if (pausePanel != null) pausePanel.SetActive(paused);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // Menu open: cursor visible and free. Gameplay: cursor hidden and locked to the center.
    void SetCursor(bool menuOpen)
    {
        Cursor.visible = menuOpen;
        Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
    }

    void OpenPanel(GameObject panel)
    {
        if (panel == null) return;
        if (pausePanel != null) pausePanel.SetActive(false);
        panel.SetActive(true);
    }

    void EndRun()
    {
        SetPaused(false);
        SetCursor(true);   // The main menu needs a visible cursor
        SceneManager.LoadScene(menuSceneName);
    }

    void MainMenu()
    {
        SetPaused(false);
        SetCursor(true);   // The main menu needs a visible cursor
        SceneManager.LoadScene(menuSceneName);
    }

    void OnDestroy()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
