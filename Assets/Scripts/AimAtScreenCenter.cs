using UnityEngine;

public class AimAtScreenCenter : MonoBehaviour
{
    [SerializeField] private Transform aimPoint;   // The AimPoint object on the camera

    private void LateUpdate()
    {
        if (aimPoint != null)
            transform.LookAt(aimPoint.position);
    }
}
