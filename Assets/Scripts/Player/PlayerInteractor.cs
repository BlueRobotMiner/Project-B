using UnityEngine;
using TMPro;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float reach = 2.5f;
    [SerializeField] private LayerMask interactMask = ~0;
    [SerializeField] private TMP_Text promptText;   // The on-screen text
    [SerializeField] private GameObject promptPanel;   // The background object that holds the text
    [SerializeField] private GameObject heldFlashlight;   // HeldFlashlight under FlashlightMount
    [SerializeField] private Animator playerAnimator;     // Man_03


    private Interactable target;

    private void Update()
    {
        // Find what's in front of the camera
        target = null;
        if (Physics.Raycast(cameraHolder.position, cameraHolder.forward, out RaycastHit hit, reach, interactMask))
            target = hit.collider.GetComponentInParent<Interactable>();

        // Show or hide the prompt
// Show or hide the whole prompt, background included
        if (promptPanel != null)
        {
            promptPanel.SetActive(target != null);
            if (target != null && promptText != null)
                promptText.text = $"Press E to {target.ActionName}";
        }


        // Use the item
        if (target != null && Input.GetKeyDown(KeyCode.E))
            Pickup(target);

    }
        private void Pickup(Interactable item)
    {
        if (item.CompareTag("Flashlight"))
        {
            heldFlashlight.SetActive(true);
            playerAnimator.SetBool("IsHolding", true);
        }

        Destroy(item.gameObject);   // Remove the floor copy
    }

}
