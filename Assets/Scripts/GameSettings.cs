// GameSettings
// Static access to the player's settings, stored in PlayerPrefs so they persist in
// WebGL between sessions. Volume sliders, quality (halves effect counts and toggles
// post-processing), screen shake, and auto-fire.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class GameSettings
{
    public static float MusicVolume
    {
        get { return PlayerPrefs.GetFloat("MusicVolume", 0.8f); }
        set { PlayerPrefs.SetFloat("MusicVolume", value); PlayerPrefs.Save(); }
    }

    public static float SfxVolume
    {
        get { return PlayerPrefs.GetFloat("SfxVolume", 0.8f); }
        set { PlayerPrefs.SetFloat("SfxVolume", value); PlayerPrefs.Save(); }
    }

    public static bool HighQuality
    {
        get { return PlayerPrefs.GetInt("Quality", 1) == 1; }
        set { PlayerPrefs.SetInt("Quality", value ? 1 : 0); PlayerPrefs.Save(); ApplyQuality(); }
    }

    public static bool ScreenShake
    {
        get { return PlayerPrefs.GetInt("ScreenShake", 1) == 1; }
        set { PlayerPrefs.SetInt("ScreenShake", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool AutoFire
    {
        get { return PlayerPrefs.GetInt("AutoFire", 0) == 1; }
        set { PlayerPrefs.SetInt("AutoFire", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Quality: on Low, halve an effect count (min 1) so explosions stay readable.
    public static int ScaleEffectCount(int count)
    {
        return HighQuality ? count : Mathf.Max(1, count / 2);
    }

    // Applies post-processing on every Volume in the scene, and again on scene changes.
    public static void ApplyQuality()
    {
        foreach (Volume v in Object.FindObjectsOfType<Volume>()) v.enabled = HighQuality;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        ApplyQuality();
        SceneManager.sceneLoaded += (scene, mode) => ApplyQuality();
    }
}
