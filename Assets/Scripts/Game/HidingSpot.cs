using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HidingSpot : MonoBehaviour
{
    // The monster's sight check will read this later. Move it onto the player when you add a proper state.
    public static bool PlayerHidden { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            PlayerHidden = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            PlayerHidden = false;
    }
}
