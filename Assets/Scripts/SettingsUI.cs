// SettingsUI
// The settings panel UI. Sliders for music/SFX, fullscreen toggle, Low/High quality
// arrows, screen shake and auto-fire toggles. Values load from GameSettings when the
// panel opens and save the moment they change. Back returns to the previous panel.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    public Slider musicSlider;
    public TMP_Text musicValueText;
    public Slider sfxSlider;
    public TMP_Text sfxValueText;
    public Toggle fullscreenToggle;
    public Button qualityLeft;
    public Button qualityRight;
    public TMP_Text qualityText;
    public Toggle screenShakeToggle;
    public Toggle autoFireToggle;
    public Button backButton;
    public GameObject returnPanel;

    void Awake()
    {
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => { GameSettings.MusicVolume = v; Refresh(); });
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => { GameSettings.SfxVolume = v; Refresh(); });
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(v => Screen.fullScreen = v);
        if (qualityLeft != null) qualityLeft.onClick.AddListener(() => { GameSettings.HighQuality = false; Refresh(); });
        if (qualityRight != null) qualityRight.onClick.AddListener(() => { GameSettings.HighQuality = true; Refresh(); });
        if (screenShakeToggle != null) screenShakeToggle.onValueChanged.AddListener(v => GameSettings.ScreenShake = v);
        if (autoFireToggle != null) autoFireToggle.onValueChanged.AddListener(v => GameSettings.AutoFire = v);
        if (backButton != null) backButton.onClick.AddListener(Back);
    }

    // Pushes the current saved values into the controls when the panel opens.
    void OnEnable()
    {
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        if (screenShakeToggle != null) screenShakeToggle.SetIsOnWithoutNotify(GameSettings.ScreenShake);
        if (autoFireToggle != null) autoFireToggle.SetIsOnWithoutNotify(GameSettings.AutoFire);
        Refresh();
    }

    void Refresh()
    {
        if (musicValueText != null) musicValueText.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
        if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";
        bool high = GameSettings.HighQuality;
        if (qualityText != null) qualityText.text = high ? "High" : "Low";
        if (qualityLeft != null) qualityLeft.interactable = high;
        if (qualityRight != null) qualityRight.interactable = !high;
    }

    void Back()
    {
        gameObject.SetActive(false);
        if (returnPanel != null) returnPanel.SetActive(true);
    }
}
