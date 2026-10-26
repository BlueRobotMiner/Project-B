// HudMeters
// Drives the Stamina and Flash Charge sliders on the HUD Canvas.
// Stamina comes from FirstPersonController. Flash charge comes from FlashDevice.
// Both sliders are read-only and fill from 0 to 1, smoothed so they glide.
using UnityEngine;
using UnityEngine.UI;

public class HudMeters : MonoBehaviour
{
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Slider flashSlider;
    [SerializeField] private FirstPersonController player;   // Leave empty to auto-find
    [SerializeField] private FlashDevice flashDevice;        // Leave empty to auto-find
    [SerializeField] private float smoothing = 10f;          // Higher = snappier

    private void Awake()
    {
        if (player == null) player = FindObjectOfType<FirstPersonController>();
        if (flashDevice == null) flashDevice = FindObjectOfType<FlashDevice>();

        Setup(staminaSlider);
        Setup(flashSlider);
    }

    private void Update()
    {
        float t = smoothing * Time.deltaTime;

        if (staminaSlider != null && player != null)
            staminaSlider.value = Mathf.Lerp(staminaSlider.value, player.StaminaNormalized, t);

        if (flashSlider != null && flashDevice != null)
            flashSlider.value = Mathf.Lerp(flashSlider.value, flashDevice.Charge01, t);
    }

    private void Setup(Slider slider)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.interactable = false;   // The player can't drag them
    }
}
