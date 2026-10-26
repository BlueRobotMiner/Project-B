// MainMenu
// Main menu buttons: Play loads the first level, Settings and Credits open their panels
// with Back returning here, and Quit exits (auto-hidden on WebGL, where Application.Quit
// does nothing).
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public string firstLevelScene = "Level 1";
    public GameObject menuPanel;
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;
    public Button creditsButton;
    public SettingsUI settings;
    public CreditsUI credits;

    void Awake()
    {
        Time.timeScale = 1f;
        if (playButton != null) playButton.onClick.AddListener(() => SceneManager.LoadScene(firstLevelScene));
        if (settingsButton != null) settingsButton.onClick.AddListener(() => OpenPanel(settings != null ? settings.gameObject : null));
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        if (creditsButton != null) creditsButton.onClick.AddListener(() => OpenPanel(credits != null ? credits.gameObject : null));
        if (credits != null) credits.returnPanel = menuPanel;
        if (settings != null) settings.returnPanel = menuPanel;
#if UNITY_WEBGL && !UNITY_EDITOR
        if (quitButton != null) quitButton.gameObject.SetActive(false);
#endif
    }

    void OpenPanel(GameObject panel)
    {
        if (panel == null) return;
        if (menuPanel != null) menuPanel.SetActive(false);
        panel.SetActive(true);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
