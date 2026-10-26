// WinMenu
// The escape screen, shown when the player escapes in the helicopter.
// Freezes the game, frees the cursor, stops player movement, and offers Main Menu and Quit.
// It sets GameOverMenu.IsGameOver so pausing and monster attacks stop, like a game over.
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WinMenu : MonoBehaviour
{
    public static WinMenu Instance;

    [SerializeField] private GameObject winPanel;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private string menuSceneName = "MainMenu";

    private FirstPersonController player;

    private void Awake()
    {
        Instance = this;
        player = FindObjectOfType<FirstPersonController>();

        if (menuButton != null) menuButton.onClick.AddListener(MainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);

        if (winPanel != null) winPanel.SetActive(false);
    }

    public void Show()
    {
        if (GameOverMenu.IsGameOver) return;

        GameOverMenu.IsGameOver = true;   // Blocks pausing, attacks, and other end-of-run logic
        Time.timeScale = 0f;
        if (player != null) player.MovementEnabled = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (winPanel != null) winPanel.SetActive(true);
    }

    private void MainMenu()
    {
        ClearState();
        SceneManager.LoadScene(menuSceneName);
    }

    private void Quit()
    {
        ClearState();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ClearState()
    {
        GameOverMenu.IsGameOver = false;
        Time.timeScale = 1f;
        Inventory.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
