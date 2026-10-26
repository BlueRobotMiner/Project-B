// GameOverMenu
// The game over overlay: freezes the game, stops player movement, and frees the cursor so
// the buttons can be clicked. Offers Restart Level (reloads the current scene) and Main Menu.
// Escape is blocked while it's up, so a dead run can't be resumed.
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverMenu : MonoBehaviour
{
    public static GameOverMenu Instance;
    public static bool IsGameOver;

    public GameObject gameOverPanel;
    [UnityEngine.Serialization.FormerlySerializedAs("retryButton")] public Button restartButton;
    public Button menuButton;
    public Button quitButton;
    public string menuSceneName = "MainMenu";

    private FirstPersonController player;

    void Awake()
    {
        Instance = this;
        IsGameOver = false;
        player = FindObjectOfType<FirstPersonController>();

        if (restartButton != null) restartButton.onClick.AddListener(RestartLevel);
        if (menuButton != null) menuButton.onClick.AddListener(Menu);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    // Called by PlayerHealth when the player dies.
    public void Show()
    {
        IsGameOver = true;
        Time.timeScale = 0f;
        if (player != null) player.MovementEnabled = false;
        SetCursor(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // Unfreezes time and reloads whatever level the player died in.
    // The reloaded scene's FirstPersonController locks the cursor again in its Awake.
    void RestartLevel()
    {
        ClearState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Menu()
    {
        ClearState();
        SceneManager.LoadScene(menuSceneName);
    }

    void Quit()
    {
        ClearState();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Menu open: cursor visible and free. Gameplay: cursor hidden and locked.
    void SetCursor(bool menuOpen)
    {
        Cursor.visible = menuOpen;
        Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
    }

    void ClearState()
    {
        IsGameOver = false;
        Time.timeScale = 1f;
        SetCursor(true);   // Leave the cursor visible for the menu; gameplay re-locks it on load
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        ClearState();
    }
}
