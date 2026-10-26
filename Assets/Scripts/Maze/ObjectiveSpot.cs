// ObjectiveSpot
// Put on an empty child of a safe room prefab. Objectives spawn at one of these spots.
// Keep spots outside any DecorGroup, or the objective could end up hidden.
using UnityEngine;

public class ObjectiveSpot : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
