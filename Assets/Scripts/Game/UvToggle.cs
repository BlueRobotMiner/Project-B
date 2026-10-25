using UnityEngine;

public class UVToggle : MonoBehaviour
{
    [SerializeField] private Light flashlight;              // The Spotlight on the flashlight
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color uvColor = new Color(0.6f, 0.2f, 1f);
    [SerializeField] private GameObject[] uvOnlyObjects;    // The notes and decals

    private bool uvOn;

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.V))
            return;

        uvOn = !uvOn;
        flashlight.color = uvOn ? uvColor : normalColor;

        foreach (GameObject obj in uvOnlyObjects)
            obj.SetActive(uvOn);
    }
}
