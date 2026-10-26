// UvToggle
// Put this on the PLAYER so it always runs.
// Finds every object tagged "UVDetail":
//   - Objects already in the scene (even inactive ones) are found when the scene loads.
//   - Objects spawned at runtime (for example by MazeBuilder) are found every frame and
//     hidden the moment they appear.
// In UV mode, only the single detail closest to the center of the screen is shown, and only
// if it's in range, lit by the flashlight beam, and not blocked.
using System.Collections.Generic;
using UnityEngine;

public class UvToggle : MonoBehaviour
{
    private const string UvTag = "UVDetail";

    [Header("Flashlight")]
    [SerializeField] private Light flashlight;                 // The Spotlight on the held flashlight
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color uvColor = new Color(0.6f, 0.2f, 1f);

    [Header("Looking (center of screen)")]
    [SerializeField] private Transform eye;                    // PlayerCamera
    [SerializeField] private Transform aimPoint;               // AimPoint
    [SerializeField] private float lookRange = 6f;
    [SerializeField] private float lookAngle = 10f;            // Degrees from the center line that still count

    [Header("Blocking")]
    [SerializeField] private float occlusionTolerance = 0.1f;  // Ignores the wall a detail sits on

    private readonly HashSet<Transform> details = new HashSet<Transform>();
    private Transform shown;
    private bool uvOn;

    public bool UvOn => uvOn;

    void Awake()
    {
        // Objects placed in the scene, including inactive ones
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.gameObject.scene.IsValid() && t.tag == UvTag)
                RegisterDetail(t);
        }
    }

    void Update()
    {
        FindNewDetails();

        if (Input.GetKeyDown(KeyCode.V) && flashlight != null && flashlight.isActiveAndEnabled)
        {
            uvOn = !uvOn;
            flashlight.color = uvOn ? uvColor : normalColor;
            AudioManager.Instance?.PlayFlashlightClick();
        }

        Transform target = uvOn ? FindDetailInView() : null;
        Show(target);
    }

    // Objects spawned at runtime start active. Anything new is hidden in the same frame.
    // Objects already registered are skipped.
    void FindNewDetails()
    {
        foreach (GameObject go in GameObject.FindGameObjectsWithTag(UvTag))
            RegisterDetail(go.transform);
    }

    void RegisterDetail(Transform t)
    {
        if (details.Add(t))
            t.gameObject.SetActive(false);   // Hidden from the start
    }

    // Shows one detail at a time and hides the previous one.
    void Show(Transform detail)
    {
        if (shown == detail) return;
        if (shown != null) shown.gameObject.SetActive(false);
        shown = detail;
        if (shown != null) shown.gameObject.SetActive(true);
    }

    // The detail closest to the center of the screen that is in range, lit, and not blocked.
    Transform FindDetailInView()
    {
        if (eye == null || aimPoint == null) return null;
        if (flashlight == null || !flashlight.isActiveAndEnabled) return null;

        Vector3 aimDirection = aimPoint.position - eye.position;
        Transform best = null;
        float bestAngle = lookAngle;

        foreach (Transform d in details)
        {
            if (d == null) continue;   // Picked up or destroyed

            Vector3 toDetail = d.position - eye.position;
            float distance = toDetail.magnitude;
            if (distance > lookRange) continue;

            float angle = Vector3.Angle(aimDirection, toDetail);
            if (angle > bestAngle) continue;

            if (!IsLit(d)) continue;
            if (!ClearLine(eye.position, toDetail, distance, d)) continue;

            best = d;
            bestAngle = angle;
        }

        return best;
    }

    // The flashlight beam reaches the detail: inside range, inside the cone, and not blocked.
    bool IsLit(Transform d)
    {
        Vector3 origin = flashlight.transform.position;
        Vector3 toDetail = d.position - origin;
        float distance = toDetail.magnitude;

        if (distance > flashlight.range) return false;
        if (Vector3.Angle(flashlight.transform.forward, toDetail) > flashlight.spotAngle * 0.5f) return false;

        return ClearLine(origin, toDetail, distance, d);
    }

    // True when nothing closer than the detail blocks the way.
    // Ignores the player's own colliders, the detail itself, and triggers (safe-room areas).
    bool ClearLine(Vector3 origin, Vector3 toTarget, float distance, Transform detail)
    {
        if (distance <= 0.001f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, toTarget / distance, distance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;   // The player, including the flashlight
            if (hit.transform.IsChildOf(detail)) continue;      // The detail itself
            return hit.distance >= distance - occlusionTolerance;
        }

        return true;
    }
}
