// Interactable
// Anything the player can press E on. Action Name shows in the prompt.
// Item Id puts the object in the inventory when picked up (for example "Camera", "Fuel").
// Can Pick Up = false means it's used in place and stays in the world (for example the helicopter).
// Description is shown on the Dialog Canvas after the player interacts.
using UnityEngine;

public class Interactable : MonoBehaviour
{
    [SerializeField] private string actionName = "pick up";
    [SerializeField] private string itemId = "";
    [SerializeField] private bool canPickUp = true;
    [SerializeField, TextArea(2, 4)] private string description = "";

    public string ActionName => actionName;
    public string ItemId => itemId;
    public bool CanPickUp => canPickUp;
    public string Description => description;
}
