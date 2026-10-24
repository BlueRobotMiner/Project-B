using System.Collections;
using UnityEngine;

public class WakeUpSequence : MonoBehaviour
{
    [SerializeField] private CanvasGroup blackOverlay;   // BlackOverlay's Canvas Group
    [SerializeField] private Transform cameraPivot;      // the Main Camera
    [SerializeField] private FirstPersonController player;
    [SerializeField] private float holdTime = 1.5f;      // seconds the screen stays black
    [SerializeField] private float fadeTime = 4f;        // seconds to fade in
    [SerializeField] private float startPitch = 60f;     // degrees looking down

    public bool IsPlaying { get; private set; }

    public IEnumerator Play()
    {
        IsPlaying = true;

        // Lock movement and mouse look until the sequence finishes
        if (player != null)
            player.MovementEnabled = false;

        blackOverlay.alpha = 1f;
        cameraPivot.localRotation = Quaternion.Euler(startPitch, 0f, 0f);

        yield return new WaitForSeconds(holdTime);

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / fadeTime);

            blackOverlay.alpha = 1f - k;
            cameraPivot.localRotation = Quaternion.Slerp(
                Quaternion.Euler(startPitch, 0f, 0f),
                Quaternion.identity,
                k);

            yield return null;
        }

        blackOverlay.alpha = 0f;

        // Give control back
        if (player != null)
            player.MovementEnabled = true;

        IsPlaying = false;
    }
}
