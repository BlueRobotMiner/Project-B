// PlayerSpawner
// Builds the maze, places the player at PlayerSpawn, then places the monster on the NavMesh at a
// reachable spot a fair walk from the player. It prefers a spot just outside a safe room (its stare
// point), then any safe room, then any walkable spot in range. It logs which one it used.
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
    [SerializeField] private float minWalkFromPlayer = 30f;   // Walking distance: not too close
    [SerializeField] private float maxWalkFromPlayer = 80f;   // Walking distance: not too far
    [SerializeField] private float snapRadius = 10f;          // How far to search for the NavMesh near a target

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

        Vector3 from = player.position;
        List<SafeRoom> rooms = new List<SafeRoom>(SafeRoom.All);
        Shuffle(rooms);

        // 1) Just outside a preferred room (A, B, C), a fair walk away
        if (TryPlaceAtRooms(from, rooms, true, minWalkFromPlayer)) return;

        // 2) Just outside any safe room, a fair walk away
        if (TryPlaceAtRooms(from, rooms, false, minWalkFromPlayer)) return;

        // 3) Any safe room at all, even if it's closer
        if (TryPlaceAtRooms(from, rooms, false, 0f)) return;

        // 4) Any walkable spot in the walking range around the player
        if (TryPlaceAround(from)) return;

        Debug.LogError("PlayerSpawner: could not find any spot for the monster. The monster stays where it is in the scene.", this);
    }

    private bool TryPlaceAtRooms(Vector3 from, List<SafeRoom> rooms, bool preferredOnly, float minWalk)
    {
        foreach (SafeRoom room in rooms)
        {
            if (preferredOnly && !IsPreferredRoom(room)) continue;

            Vector3 target = room.StarePoint != null ? room.StarePoint.position : room.transform.position;
            if (!SnapToNavMesh(target, out Vector3 spot)) continue;

            float walk = PathDistance(spot, from);
            if (walk == float.MaxValue || walk < minWalk || walk > maxWalkFromPlayer * 2f) continue;

            // NEW: reject spots that are much farther by walking than in a straight line (usually across a wall)
            float straight = Vector3.Distance(spot, from);
            if (walk > straight * 2f + 4f) continue;

            PlaceMonster(spot);
            Debug.Log($"PlayerSpawner: monster placed outside '{room.gameObject.name}', {walk:F0} units of walking from the player");
            return true;
        }

        return false;
    }

    private bool TryPlaceAround(Vector3 from)
    {
        for (int i = 0; i < 300; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(minWalkFromPlayer, maxWalkFromPlayer);
            Vector3 candidate = from + new Vector3(offset.x, 0f, offset.y);

            if (!SnapToNavMesh(candidate, out Vector3 spot)) continue;

            float walk = PathDistance(spot, from);
            if (walk == float.MaxValue || walk < minWalkFromPlayer || walk > maxWalkFromPlayer) continue;

            // NEW: reject spots that are much farther by walking than in a straight line (usually across a wall)
            float straight = Vector3.Distance(spot, from);
            if (walk > straight * 2f + 4f) continue;

            PlaceMonster(spot);
            Debug.Log($"PlayerSpawner: monster placed on the NavMesh, {walk:F0} units of walking from the player");
            return true;
        }

        return false;
    }

    private void PlaceMonster(Vector3 position)
    {
        NavMeshAgent agent = monster.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;   // Reset the agent so it's created on the new NavMesh
            agent.enabled = true;
            if (!agent.Warp(position))
                Debug.LogWarning("PlayerSpawner: the agent couldn't warp to the spot, so it may not move", this);
        }
        else
        {
            monster.position = position;
        }
    }
    // Snaps a point onto the NavMesh, but only if it's close to the target and reachable without a big detour
    private bool SnapToNavMesh(Vector3 target, out Vector3 spot)
    {
        spot = target;

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, snapRadius, NavMesh.AllAreas))
            return false;

        if (Vector3.Distance(hit.position, target) > snapRadius)
            return false;

        spot = hit.position;
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
