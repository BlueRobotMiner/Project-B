// PlayerSpawner
// Builds the maze, places the player at PlayerSpawn, then places the monster just outside
// one of the objective safe rooms (A, B, C), at that room's Stare Point, snapped onto the NavMesh.
// Rooms too close to the player (by walking distance) are skipped.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private MazeBuilder mazeBuilder;
    [SerializeField] private Transform player;          // The Player object
    [SerializeField] private Transform cameraPivot;     // The camera, for the wake-up look
    [SerializeField] private WakeUpSequence wakeUp;
    [SerializeField] private string spawnName = "PlayerSpawn";
    [SerializeField] private float spawnLift = 0.1f;

    [Header("Monster")]
    [SerializeField] private Transform monster;
    [SerializeField] private string[] preferredRoomNames = { "Safe area A", "Safe area B", "Safe area C" };
    [SerializeField] private float minWalkFromPlayer = 40f;  // Skip rooms closer than this (by walking distance)
    [SerializeField] private float snapRadius = 4f;          // How far to search for the NavMesh near a Stare Point

    private void Start()
    {
        // Build the maze first, so the rooms, NavMesh, and spawn points all exist
        mazeBuilder.Build();

        PlacePlayer();
        PlaceMonster();

        StartCoroutine(wakeUp.Play());
    }

    private void PlacePlayer()
    {
        GameObject spawnObject = GameObject.Find(spawnName);
        if (spawnObject == null)
        {
            Debug.LogError("PlayerSpawner: no object named '" + spawnName + "' found after building the maze", this);
            return;
        }

        Transform spawn = spawnObject.transform;

        Vector3 pos = spawn.position;
        player.position = new Vector3(pos.x, pos.y + spawnLift, pos.z);
        player.rotation = Quaternion.Euler(0f, spawn.eulerAngles.y, 0f);
    }

    private void PlaceMonster()
    {
        if (monster == null)
        {
            Debug.LogWarning("PlayerSpawner: no monster assigned", this);
            return;
        }

        IReadOnlyList<SafeRoom> rooms = SafeRoom.All;
        if (rooms.Count == 0)
        {
            Debug.LogError("PlayerSpawner: no safe rooms were found to spawn the monster near", this);
            return;
        }

        // Prefer the objective rooms (A, B, C). If none match, use any safe room.
        List<SafeRoom> candidates = new List<SafeRoom>();
        foreach (SafeRoom room in rooms)
        {
            if (IsPreferredRoom(room))
                candidates.Add(room);
        }
        if (candidates.Count == 0)
            candidates.AddRange(rooms);

        Shuffle(candidates);   // A different room each run

        Vector3 from = player.position;

        // First pass: a room far enough away from the player
        foreach (SafeRoom room in candidates)
        {
            if (TryPlaceNearRoom(room, from, minWalkFromPlayer)) return;
        }

        // Second pass: any reachable room, even if it's close
        foreach (SafeRoom room in candidates)
        {
            if (TryPlaceNearRoom(room, from, 0f)) return;
        }

        Debug.LogError("PlayerSpawner: could not reach any safe room from the player's spawn", this);
    }

    private bool TryPlaceNearRoom(SafeRoom room, Vector3 from, float minWalk)
    {
        Vector3 stare = room.StarePoint != null ? room.StarePoint.position : room.transform.position;

        if (!NavMesh.SamplePosition(stare, out NavMeshHit hit, snapRadius, NavMesh.AllAreas))
            return false;

        float walk = PathDistance(hit.position, from);
        if (walk == float.MaxValue || walk < minWalk)
            return false;

        WarpMonster(hit.position);
        Debug.Log($"PlayerSpawner: monster placed near '{room.gameObject.name}', {walk:F0} units of walking from the player");
        return true;
    }

    private bool IsPreferredRoom(SafeRoom room)
    {
        foreach (string name in preferredRoomNames)
        {
            if (room.gameObject.name.StartsWith(name))
                return true;
        }
        return false;
    }

    private void WarpMonster(Vector3 position)
    {
        NavMeshAgent agent = monster.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;   // Reset the agent so it's created on the new NavMesh
            agent.enabled = true;
            agent.Warp(position);
        }
        else
        {
            monster.position = position;
        }
    }

    // Walking distance along the NavMesh. Returns a huge number if there's no path.
    private float PathDistance(Vector3 from, Vector3 to)
    {
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            return float.MaxValue;

        float total = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            total += Vector3.Distance(path.corners[i - 1], path.corners[i]);

        return total;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
