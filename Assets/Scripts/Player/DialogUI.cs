// DialogUI
// Shows a short description on the Dialog Canvas after an item is picked up or used.
// Put this on the Dialog Canvas root. Its Panel is the background object that turns on and off.
using System.Collections;
using TMPro;
using UnityEngine;

public class DialogUI : MonoBehaviour
{
    public static DialogUI Instance;

    [SerializeField] private GameObject panel;     // The background object that shows and hides
    [SerializeField] private TMP_Text text;        // The text inside the panel
    [SerializeField] private float showSeconds = 4f;

    private Coroutine routine;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message) || panel == null) return;

        if (text != null) text.text = message;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        panel.SetActive(true);
        yield return new WaitForSeconds(showSeconds);
        panel.SetActive(false);
        routine = null;
    }
}
