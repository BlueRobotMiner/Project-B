// SafeRoom
// Put on a room prefab. Give the room's root a Box Collider covering the room's floor
// and walls (it doesn't have to block movement). While the player is inside with the
// lights on, monsters wait at the Stare Point. If the player stays long enough, the
// room's street lights turn off, and the room opens up for the monster.
using System;
using System.Collections.Generic;
using UnityEngine;

public class SafeRoom : MonoBehaviour
{
    private static readonly List<SafeRoom> all = new List<SafeRoom>();

    // Fired once when the lights go off. MazeBuilder uses this to open the room's NavMesh.
    public static event Action<SafeRoom> LightsOff;

    [SerializeField] private Collider area;               // Covers the room (defaults to this object's collider)
    [SerializeField] private Transform starePoint;        // Where the monster waits outside the room
    [SerializeField] private Light[] streetLights;        // The street lights in this room's area
    [SerializeField] private float lightOffDelay = 5f;    // Seconds the player can stay before the lights go out

    private Transform player;
    private float timeInside;

    public static IReadOnlyList<SafeRoom> All => all;
    public Transform StarePoint => starePoint;
    public bool LightsOn { get; private set; } = true;

    private void Awake()
    {
        if (area == null) area = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    private void Start()
    {
        FirstPersonController fpc = FindObjectOfType<FirstPersonController>();
        if (fpc != null) player = fpc.transform;
    }

    private void Update()
    {
        if (!LightsOn || player == null || area == null) return;

        if (area.bounds.Contains(player.position))
        {
            timeInside += Time.deltaTime;
            if (timeInside >= lightOffDelay) TurnOffLights();
        }
        else
        {
            timeInside = 0f;   // Leaving the room resets the timer
        }
    }

    // Returns the safe room the point is inside, or null.
    public static SafeRoom Containing(Vector3 point)
    {
        foreach (SafeRoom room in all)
        {
            if (room.area != null && room.area.bounds.Contains(point)) return room;
        }
        return null;
    }

    public void TurnOffLights()
    {
        if (!LightsOn) return;

        LightsOn = false;
        foreach (Light light in streetLights)
        {
            if (light != null) light.enabled = false;
        }

        LightsOff?.Invoke(this);
    }
}
