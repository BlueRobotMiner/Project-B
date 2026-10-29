// FrameLimiter
// Caps the frame rate. Put on any object in the first scene (for example MainMenu).
// Set Target Frame Rate in the Inspector. Use -1 for no cap.
using UnityEngine;

public class FrameLimiter : MonoBehaviour
{
    [SerializeField] private int targetFrameRate = 60;

    private void Awake()
    {
        // Stops the game from running uncapped, which can make the game and the browser stutter
        QualitySettings.vSyncCount = 0;   // Must be off for targetFrameRate to take effect
        Application.targetFrameRate = targetFrameRate;
    }
}
