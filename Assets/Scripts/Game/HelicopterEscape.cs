// HelicopterEscape
// Put on the helicopter, with an Interactable on the same object (Can Pick Up unchecked).
// E escapes only if every required item is in the inventory. Then the Win Menu shows.
using UnityEngine;

public class HelicopterEscape : MonoBehaviour
{
    [SerializeField] private string[] requiredItems = { "TailRotor", "Fuel", "Keys" };

    private bool escaped;

    public void Interact()
    {
        if (escaped) return;

        foreach (string id in requiredItems)
        {
            if (!Inventory.Has(id))
            {
                string message = "The helicopter still needs: " + id;
                Debug.Log(message);
                DialogUI.Instance?.Show(message);
                return;
            }
        }

        escaped = true;

        if (WinMenu.Instance != null)
            WinMenu.Instance.Show();
        else
            Debug.LogError("HelicopterEscape: no WinMenu in the scene. Add one to the Win Menu canvas.");
    }
}
