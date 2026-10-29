// ScreenFlash
// A camera-style flash: the screen goes bright white instantly, then fades out quickly
// with a slow tail. Put on the Flash object under Flash Canvas. The Flash object needs an
// Image, stretched to fill the screen, and it should stay ACTIVE. Its alpha is set to 0 at start.
using UnityEngine;
using UnityEngine.UI;

public class ScreenFlash : MonoBehaviour
{
    public static ScreenFlash Instance;

    [SerializeField] private Image image;              // Leave empty to use the Image on this object
    [SerializeField] private float fadeSeconds = 1.2f; // How long the flash takes to fade out
    [SerializeField] private float peakAlpha = 1f;     // Brightness at the start of the flash

    private float timer;
    private bool flashing;

    private void Awake()
    {
        Instance = this;
        if (image == null) image = GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = false;   // Never block clicks
            SetAlpha(0f);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Play()
    {
        if (image == null) return;
        timer = 0f;
        flashing = true;
        SetAlpha(peakAlpha);
    }

    private void Update()
    {
        if (!flashing) return;

        timer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(timer / fadeSeconds);

        // Fast drop at first, then a slow tail: like a real flash bang
        float alpha = peakAlpha * (1f - t) * (1f - t);
        SetAlpha(alpha);

        if (t >= 1f)
        {
            flashing = false;
            SetAlpha(0f);
        }
    }

    private void SetAlpha(float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
