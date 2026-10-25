using System.Collections;
using UnityEngine;

public class WakeUpSequence : MonoBehaviour
{
    [SerializeField] private CanvasGroup blackOverlay;   // BlackOverlay's Canvas Group
    [SerializeField] private Transform cameraPivot;      // the Main Camera
    [SerializeField] private FirstPersonController player;
    [SerializeField] private Animator playerAnimator;    // Drag Man_03 here
    [SerializeField] private string wakeUpLayerName = "WakeUp";
    [SerializeField] private float holdTime = 1.5f;      // seconds the screen stays black
    [SerializeField] private float fadeTime = 4f;        // seconds to fade in
    [SerializeField] private float startPitch = 60f;     // degrees looking down
    [SerializeField] private float blendTime = 0.4f;     // seconds to blend the WakeUp layer out

    public bool IsPlaying { get; private set; }

    public IEnumerator Play()
    {
        IsPlaying = true;

        // Lock movement and mouse look until the sequence finishes
        if (player != null)
            player.MovementEnabled = false;

        // Turn the WakeUp layer on and start the stand-up
        int layer = -1;
        if (playerAnimator != null)
        {
            layer = playerAnimator.GetLayerIndex(wakeUpLayerName);
            playerAnimator.SetLayerWeight(layer, 1f);
            playerAnimator.SetTrigger("WakeUp");
        }

        blackOverlay.alpha = 1f;
        cameraPivot.localRotation = Quaternion.Euler(startPitch, 0f, 0f);

        yield return new WaitForSeconds(holdTime);

        // Fade in the screen
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

        // Wait until the Standing up clip has fully finished
        if (playerAnimator != null)
        {
            yield return new WaitUntil(() =>
            {
                AnimatorStateInfo info = playerAnimator.GetCurrentAnimatorStateInfo(layer);
                return info.IsName("Standing up") && info.normalizedTime >= 1f;
            });

            // Blend the layer out, so the base pose takes over smoothly
            float b = 0f;
            while (b < blendTime)
            {
                b += Time.deltaTime;
                playerAnimator.SetLayerWeight(layer, 1f - b / blendTime);
                yield return null;
            }

            playerAnimator.SetLayerWeight(layer, 0f);
        }

        // Give control back
        if (player != null)
            player.MovementEnabled = true;

        IsPlaying = false;
    }
}
