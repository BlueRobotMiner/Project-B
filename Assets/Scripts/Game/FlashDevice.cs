// FlashDevice
// The digital camera flash. Needs the "Camera" item. Press F when the charge is full:
// the screen flashes (ScreenFlash), and monsters in front of the camera retreat.
// Debug Logs prints each step, so you can see where the flash stops.
using System.Collections.Generic;
using UnityEngine;

public class FlashDevice : MonoBehaviour
{
    [SerializeField] private Transform eye;                 // The camera (PlayerCamera). Leave empty to use the main camera.
    [SerializeField] private float range = 30f;
    [SerializeField] private float coneHalfAngle = 50f;
    [SerializeField] private float retreatSeconds = 120f;   // How long a flashed monster stays away
    [SerializeField] private float rechargeSeconds = 8f;    // Time to refill one full charge
    [SerializeField] private KeyCode flashKey = KeyCode.F;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private float charge = 1f;   // 0 = empty, 1 = ready

    public float Charge01 => charge;   // Read by the HUD slider

    private void Awake()
    {
        if (eye == null && Camera.main != null)
            eye = Camera.main.transform;
        if (eye == null)
            eye = transform;

        if (debugLogs)
            Debug.Log($"FlashDevice: eye is '{eye.name}'");
    }

    private void Update()
    {
        charge = Mathf.MoveTowards(charge, 1f, Time.deltaTime / rechargeSeconds);

        if (!Input.GetKeyDown(flashKey))
            return;

        bool hasCamera = Inventory.Has("Camera");
        bool ready = charge >= 1f;

        if (debugLogs)
            Debug.Log($"FlashDevice: F pressed. Has camera: {hasCamera}, charge: {charge:F2}, ready: {ready}");

        if (!hasCamera || !ready)
            return;

        Flash();
    }

    private void Flash()
    {
        charge = 0f;

        AudioManager.Instance?.PlayFlash();
        ScreenFlash.Instance?.Play();

        if (debugLogs && ScreenFlash.Instance == null)
            Debug.LogWarning("FlashDevice: no ScreenFlash in the scene, so there's no flash on screen");

        // Each monster is flashed once, even if several of its colliders are in range
        HashSet<MonsterAI> flashed = new HashSet<MonsterAI>();

        Collider[] hits = Physics.OverlapSphere(eye.position, range);
        if (debugLogs)
            Debug.Log($"FlashDevice: {hits.Length} colliders in range {range}");

        foreach (Collider hit in hits)
        {
            MonsterAI monster = hit.GetComponentInParent<MonsterAI>();
            if (monster == null || flashed.Contains(monster)) continue;

            Vector3 toMonster = monster.transform.position - eye.position;
            float angle = Vector3.Angle(eye.forward, toMonster);

            if (debugLogs)
                Debug.Log($"FlashDevice: found monster '{monster.name}', distance {toMonster.magnitude:F1}, angle {angle:F0}");

            if (angle <= coneHalfAngle)
            {
                flashed.Add(monster);
                monster.Retreat(retreatSeconds);
                if (debugLogs)
                    Debug.Log($"FlashDevice: retreat sent to '{monster.name}'");
            }
        }

        if (debugLogs && flashed.Count == 0)
            Debug.Log("FlashDevice: no monster was in the flash cone");
    }
}
