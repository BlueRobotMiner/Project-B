using UnityEngine;

public class PickupItem : MonoBehaviour
{
    [SerializeField] private Animator playerAnimator;

    public void PlayPickupAnimation()
    {
        playerAnimator.SetTrigger("Pickup");
    }
}
