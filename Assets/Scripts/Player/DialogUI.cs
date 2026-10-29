// DialogUI
// Shows one line at a time on the Dialog Canvas. Lines are queued, so they never overlap.
// Holds the pickup lines (matched by Item Id) and the opening "Where am I?" line.
// Put on the Dialog Canvas root.
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DialogUI : MonoBehaviour
{
    [Serializable]
    public class ItemLine
    {
        public string itemId;               // Must match the Interactable's Item Id
        [TextArea(2, 4)] public string text;
    }

    public static DialogUI Instance;

    [SerializeField] private GameObject panel;         // The background object that shows and hides
    [SerializeField] private TMP_Text text;            // The auto-scaling text inside the panel

    [Header("Timing")]
    [SerializeField] private float showSeconds = 4f;   // How long each line stays up
    [SerializeField] private float gapSeconds = 0.6f;  // Pause between lines
    [SerializeField] private float wakeDelay = 3f;     // Wait after the scene starts before "Where am I?"

    [Header("Lines")]
    [SerializeField] private string wakeLine = "Where am I?";
    [SerializeField] private ItemLine[] itemLines =
    {
        new ItemLine { itemId = "Camera",    text = "Maybe I can use this to my advantage. Press F to flash." },
        new ItemLine { itemId = "Flashlight", text = "A flashlight. Press V to switch to UV light, and look closely at what it reveals." },
        new ItemLine { itemId = "Keys",      text = "A set of keys. Something in this place must be locked." },
        new ItemLine { itemId = "Fuel",      text = "A gas tank. I wonder what I need to use to feel it. And how am I carrying this?" },
        new ItemLine { itemId = "TailRotor", text = "A tail rotor. There must be a crashed helicopter somewhere." }
    };

    private readonly Queue<string> queue = new Queue<string>();
    private Coroutine routine;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(WakeUpLine());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private IEnumerator WakeUpLine()
    {
        yield return new WaitForSeconds(wakeDelay);
        Show(wakeLine);
    }

    // Shows the line for a picked-up item. Falls back to the Interactable's own description.
    public void ShowItem(string itemId, string fallback)
    {
        foreach (ItemLine line in itemLines)
        {
            if (line.itemId == itemId)
            {
                Show(line.text);
                return;
            }
        }

        Show(fallback);
    }

    // Adds a line to the queue. It plays after any line that's already showing.
    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        queue.Enqueue(message);
        if (routine == null)
            routine = StartCoroutine(PlayQueue());
    }

    private IEnumerator PlayQueue()
    {
        while (queue.Count > 0)
        {
            if (text != null) text.text = queue.Dequeue();
            if (panel != null) panel.SetActive(true);

            yield return new WaitForSeconds(showSeconds);

            if (panel != null) panel.SetActive(false);
            yield return new WaitForSeconds(gapSeconds);
        }

        routine = null;
    }
}
