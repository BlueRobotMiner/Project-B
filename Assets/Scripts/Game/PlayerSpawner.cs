using UnityEngine;
using UnityEngine.AI;

// PlayerSpawner
// Builds the maze once, then places the player at the starter room's PlayerSpawn
// and the monster at the MazeBuilder's monster spawn point (snapped onto the NavMesh).
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private MazeBuilder mazeBuilder;
    [SerializeField] private Transform player;          // the Player object
    [SerializeField] private Transform cameraPivot;     // the Main Camera, for the wake-up look
    [SerializeField] private WakeUpSequence wakeUp;
    [SerializeField] private string spawnName = "PlayerSpawn";   // Name of the spawn empty in the generated starter room
    [SerializeField] private float spawnLift = 0.1f;             // Small lift so the player doesn't start inside the floor

    [Header("Monster")]
    [SerializeField] private Transform monster;                  // The monster object in the scene
    [SerializeField] private float navSnapRadius = 3f;           // How far to search for the NavMesh near the spawn point

    private void Start()
    {
        // Build the maze first, so the starter room, PlayerSpawn, NavMesh, and monster spawn point all exist
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

        // Put the player at the spawn point
        Vector3 pos = spawn.position;
        player.position = new Vector3(pos.x, pos.y + spawnLift, pos.z);

        // Use only the spawn's Y rotation, so the player stays upright
        player.rotation = Quaternion.Euler(0f, spawn.eulerAngles.y, 0f);
    }

    private void PlaceMonster()
    {
        if (monster == null)
        {
            Debug.LogWarning("PlayerSpawner: no monster assigned", this);
            return;
        }

        // The point the maze builder picked, snapped onto the baked NavMesh
        Vector3 target = mazeBuilder.MonsterSpawnWorld;
        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, navSnapRadius, NavMesh.AllAreas))
        {
            Debug.LogError("PlayerSpawner: the monster spawn point is not on the NavMesh", this);
            return;
        }
        

        // Warping moves a NavMeshAgent safely onto the mesh; a plain move would leave it stranded
        NavMeshAgent agent = monster.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;   // Reset the agent so it's created on the new NavMesh
            agent.enabled = true;
            agent.Warp(hit.position);
        }

        else
            monster.position = hit.position;
    }
}
