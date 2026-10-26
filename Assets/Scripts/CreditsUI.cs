// CreditsUI
// Scrolling credits: the Content box slides upward through a Rect Mask 2D viewport
// after a short delay, then either loops or stops at the end. Works while the game
// is paused (unscaled time). Back returns to the previous panel.
using UnityEngine;
using UnityEngine.UI;

public class CreditsUI : MonoBehaviour
{
    public RectTransform content;
    public float scrollSpeed = 40f;
    public float startDelay = 1f;
    public bool loop = true;
    public Button backButton;
    public GameObject returnPanel;

    private float startY;
    private float timer;

    void Awake()
    {
        if (content != null) startY = content.anchoredPosition.y;
        if (backButton != null) backButton.onClick.AddListener(Back);
    }

    void OnEnable()
    {
        timer = 0f;
        SetY(startY);
    }

    // Scrolls Content up through its viewport, looping or stopping at the end.
    void Update()
    {
        if (content == null) return;
        timer += Time.unscaledDeltaTime;
        if (timer < startDelay) return;

        float y = content.anchoredPosition.y + scrollSpeed * Time.unscaledDeltaTime;
        RectTransform viewport = content.parent as RectTransform;
        float endY = startY + content.rect.height + (viewport != null ? viewport.rect.height : 0f);
        if (y >= endY)
        {
            if (!loop) return;
            y = startY;
        }
        SetY(y);
    }

    void SetY(float y)
    {
        if (content == null) return;
        Vector2 pos = content.anchoredPosition;
        pos.y = y;
        content.anchoredPosition = pos;
    }

    void Back()
    {
        gameObject.SetActive(false);
        if (returnPanel != null) returnPanel.SetActive(true);
    }
}
