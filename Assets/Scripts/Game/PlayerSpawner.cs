using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private MazeBuilder mazeBuilder;
    [SerializeField] private Transform player;          // the Player object
    [SerializeField] private Transform cameraPivot;     // the Main Camera, for the wake-up look
    [SerializeField] private WakeUpSequence wakeUp;
    [SerializeField] private string spawnName = "PlayerSpawn";   // Name of the spawn empty in the generated starter room
    [SerializeField] private float spawnLift = 0.1f;             // Small lift so the player doesn't start inside the floor

    private void Start()
    {
        // Build the maze first, so the starter room and its PlayerSpawn exist
        mazeBuilder.Build();

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

        StartCoroutine(wakeUp.Play());
    }
}
