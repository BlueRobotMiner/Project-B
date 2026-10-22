using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private MazeBuilder mazeBuilder;
    [SerializeField] private Transform player;      // the Player object
    [SerializeField] private Transform cameraPivot; // the Main Camera, for the wake-up look

    private void Start()
    {
        mazeBuilder.Build();

        Vector3 spawn = mazeBuilder.PlayerSpawnWorld;

        // Small lift so the player doesn't start inside the floor
        player.position = new Vector3(spawn.x, 0.1f, spawn.z);

        // Face a random direction, so the start doesn't always look the same
        player.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }
}
