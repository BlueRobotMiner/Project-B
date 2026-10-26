// FlashDevice
// The digital camera flash. Needs the "Camera" item in the inventory.
// Press F: the Flash visual shows briefly every time. When the charge is full,
// monsters in front of the camera are stunned. The charge refills over rechargeSeconds.
using System.Collections;
using UnityEngine;

public class FlashDevice : MonoBehaviour
{
    [SerializeField] private Transform eye;                 // The camera (PlayerCamera). Leave empty to use the main camera.
    [SerializeField] private float range = 12f;
    [SerializeField] private float coneHalfAngle = 45f;
    [SerializeField] private float stunSeconds = 4f;
    [SerializeField] private float rechargeSeconds = 8f;
    [SerializeField] private KeyCode flashKey = KeyCode.F;

    [Header("Feedback")]
    [SerializeField] private GameObject flashVisual;        // Leave empty to auto-find the object named "Flash"
    [SerializeField] private float flashVisualTime = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private float charge = 1f;   // 0 = empty, 1 = ready
    private Coroutine visualRoutine;

    public float Charge01 => charge;   // For a HUD slider later

    private void Awake()
    {
        if (eye == null && Camera.main != null)
            eye = Camera.main.transform;
        if (eye == null)
            eye = transform;

        if (flashVisual == null)
            flashVisual = FindFlashVisual();

        if (flashVisual != null)
            flashVisual.SetActive(false);
    }

    private void Update()
    {
        charge = Mathf.MoveTowards(charge, 1f, Time.deltaTime / rechargeSeconds);

        if (!Input.GetKeyDown(flashKey))
            return;

        bool hasCamera = Inventory.Has("Camera");

        if (debugLogs)
            Debug.Log($"Flash key pressed. Has camera: {hasCamera}, charge: {charge:F2}, visual: {(flashVisual != null ? flashVisual.name : "MISSING")}");

        if (!hasCamera)
            return;

        ShowFlashVisual();

        if (charge >= 1f)
            Flash();
    }

    private void Flash()
    {
        charge = 0f;
        AudioManager.Instance?.PlayFlash();

        Collider[] hits = Physics.OverlapSphere(eye.position, range);
        foreach (Collider hit in hits)
        {
            MonsterAI monster = hit.GetComponentInParent<MonsterAI>();
            if (monster == null) continue;

            Vector3 toMonster = monster.transform.position - eye.position;
            if (Vector3.Angle(eye.forward, toMonster) <= coneHalfAngle)
                monster.Stun(stunSeconds);
        }
    }

    private void ShowFlashVisual()
    {
        if (flashVisual == null) return;

        if (visualRoutine != null)
            StopCoroutine(visualRoutine);

        visualRoutine = StartCoroutine(FlashVisualRoutine());
    }

    private IEnumerator FlashVisualRoutine()
    {
        flashVisual.SetActive(true);
        yield return new WaitForSeconds(flashVisualTime);
        flashVisual.SetActive(false);
        visualRoutine = null;
    }

    // Finds the "Flash" object in the scene, including inactive ones (for example under Flash Canvas)
    private GameObject FindFlashVisual()
    {
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == "Flash" && t.gameObject.scene.IsValid())
                return t.gameObject;
        }
        return null;
    }
}
