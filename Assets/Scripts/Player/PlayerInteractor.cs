using UnityEngine;
using TMPro;

// PlayerInteractor
// Finds the interactable closest to the center of the view, within reach and the aim radius.
// It must also be visible: a straight ray from the camera must reach it without hitting a wall.
// Picks up items that can be picked up, uses the helicopter, and shows each item's
// description on the Dialog Canvas.
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float reach = 2.5f;
    [SerializeField] private float aimRadius = 0.25f;     // Size of the forgiving area in front of the camera
    [SerializeField] private LayerMask interactMask = ~0;
    [SerializeField] private TMP_Text promptText;         // The on-screen text
    [SerializeField] private GameObject promptPanel;      // The background object that holds the text
    [SerializeField] private GameObject heldFlashlight;   // HeldFlashlight under FlashlightMount
    [SerializeField] private Animator playerAnimator;     // Man_03

    private Interactable target;

    private void Update()
    {
        target = FindTarget();

        // Show or hide the whole prompt, background included
        if (promptPanel != null)
        {
            promptPanel.SetActive(target != null);
            if (target != null && promptText != null)
                promptText.text = $"Press E to {target.ActionName}";
        }

        if (target != null && Input.GetKeyDown(KeyCode.E))
            Use(target);
    }

    private Interactable FindTarget()
    {
        Vector3 origin = cameraHolder.position;
        Vector3 forward = cameraHolder.forward;

        // Sweep a sphere to find every interactable in range
        RaycastHit[] hits = Physics.SphereCastAll(
            origin, aimRadius, forward, reach,
            interactMask, QueryTriggerInteraction.Collide);

        // Keep the one closest to the center of the view
        Interactable best = null;
        Vector3 bestPoint = Vector3.zero;
        float bestAngle = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;   // Ignore the player's own colliders

            Interactable item = hit.collider.GetComponentInParent<Interactable>();
            if (item == null) continue;

            float angle = Vector3.Angle(forward, hit.point - origin);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = item;
                bestPoint = hit.point;
            }
        }

        if (best == null) return null;

        // Only count it if nothing solid is between the camera and the item
        return HasLineOfSight(origin, bestPoint, best) ? best : null;
    }

    // A straight ray from the camera to the item. A wall or object in front blocks it.
    private bool HasLineOfSight(Vector3 origin, Vector3 point, Interactable item)
    {
        Vector3 direction = point - origin;
        float distance = direction.magnitude;
        if (distance < 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(
            origin, direction / distance, distance + 0.05f,
            interactMask, QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;        // The player's own colliders
            return hit.transform.IsChildOf(item.transform);           // First real hit must be the item
        }

        return true;
    }

    private void Use(Interactable item)
    {
        // The helicopter can be on the same object or a child of the interactable
        HelicopterEscape helicopter = item.GetComponent<HelicopterEscape>();
        if (helicopter == null)
            helicopter = item.GetComponentInChildren<HelicopterEscape>();

        if (helicopter != null)
        {
            helicopter.Interact();
            return;
        }

        // Things that can't be picked up stay in the world and just show their description
        if (!item.CanPickUp)
        {
            DialogUI.Instance?.Show(item.Description);
            return;
        }

        AudioManager.Instance?.PlayPickup();

        if (item.CompareTag("Flashlight"))
        {
            heldFlashlight.SetActive(true);
            playerAnimator.SetBool("IsHolding", true);
        }

        // Use the Item Id from the Inspector. If it was left empty on the camera,
        // fall back to the object's name so the camera still unlocks the flash.
        string id = item.ItemId;
        if (string.IsNullOrEmpty(id) && item.name.ToLower().Contains("camera"))
            id = "Camera";

        if (!string.IsNullOrEmpty(id))
        {
            Inventory.Add(id);
            Debug.Log($"Added '{id}' to the inventory from '{item.name}'");
        }
        DialogUI.Instance?.ShowItem(id, item.Description);
        Destroy(item.gameObject);   // Remove the floor copy
    }
}
