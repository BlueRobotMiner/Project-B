using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MazeBuilder : MonoBehaviour
{
    [SerializeField] private int cellsX = 10;
    [SerializeField] private int cellsY = 10;
    [SerializeField] private int seed = 0;               // 0 = random every run
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private float wallHeight = 2.5f;
    [SerializeField] private GameObject wallPrefab;      // your CornWall prefab
    [SerializeField] private Transform mazeRoot;         // the MazeRoot object itself
    [SerializeField] private bool bakeNavMesh = true;
    [SerializeField] private int startRoomRadius = 1;    // 1 = a 3x3 cell room
    [SerializeField] private int minMonsterDistance = 30; // in tiles
    [SerializeField] private GameObject startRoomPrefab; // optional visuals for the room

    // Spawn positions in world space, for other scripts to use later
    public Vector3 PlayerSpawnWorld { get; private set; }
    public Vector3 MonsterSpawnWorld { get; private set; }

    private MazeGrid grid;
    private NavMeshDataInstance navInstance;

    [SerializeField] private bool buildOnStart = false; // the spawner builds it instead

    private void Start()
    {
        if (buildOnStart)
            Build();
    }

    public void Build()
    {
        ClearOldMaze();

        int usedSeed = seed == 0 ? Random.Range(1, int.MaxValue) : seed;
        grid = MazeGenerator.Generate(cellsX, cellsY, usedSeed, startRoomRadius);

        // Player starts in the center of the start room
        Vector2Int playerTile = new Vector2Int(2 * (cellsX / 2) + 1, 2 * (cellsY / 2) + 1);
        MazeSpawnPlan plan = MazeSpawns.Plan(grid, playerTile, usedSeed, minMonsterDistance);

        SpawnWalls();

        PlayerSpawnWorld = TileToWorld(plan.PlayerTile);
        MonsterSpawnWorld = TileToWorld(plan.MonsterTile);

        if (startRoomPrefab != null)
        {
            GameObject room = Instantiate(startRoomPrefab, mazeRoot);
            room.transform.localPosition = TileToLocal(plan.PlayerTile);
        }

        if (bakeNavMesh)
            BakeNavMesh();

        Debug.Log($"Maze built with seed {usedSeed}. Player {plan.PlayerTile}, monster {plan.MonsterTile}");
    }

    private void ClearOldMaze()
    {
        // DestroyImmediate so old walls are gone before the NavMesh bake.
        // Keep the floor OUTSIDE MazeRoot, or it will be deleted too.
        for (int i = mazeRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(mazeRoot.GetChild(i).gameObject);
    }

    private void SpawnWalls()
    {
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                if (!grid.Walls[x, y])
                    continue;

                GameObject wall = Instantiate(wallPrefab, mazeRoot);
                wall.transform.localPosition = TileToLocal(new Vector2Int(x, y)) + new Vector3(0f, wallHeight * 0.5f, 0f);
                wall.transform.localScale = new Vector3(cellSize, wallHeight, cellSize);
            }
        }
    }

    private Vector3 TileToLocal(Vector2Int tile)
    {
        return new Vector3(tile.x * cellSize, 0f, tile.y * cellSize);
    }

    private Vector3 TileToWorld(Vector2Int tile)
    {
        return mazeRoot.position + TileToLocal(tile);
    }

    private void BakeNavMesh()
    {
        // Remove the previous maze's NavMesh before baking the new one
        navInstance.Remove();

        // Area that covers the whole maze
        float worldWidth = grid.Width * cellSize;
        float worldDepth = grid.Height * cellSize;
        Vector3 center = mazeRoot.position + new Vector3(worldWidth * 0.5f, 0f, worldDepth * 0.5f);
        Bounds bounds = new Bounds(center, new Vector3(worldWidth, 10f, worldDepth));

        // Collect every mesh inside the bounds (walls and floor)
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(
            bounds,
            ~0,                                  // all layers for now
            NavMeshCollectGeometry.RenderMeshes,
            0,                                   // default walkable area
            new List<NavMeshBuildMarkup>(),
            sources);

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            NavMesh.GetSettingsByID(0),          // default humanoid agent
            sources,
            bounds,
            Vector3.zero,
            Quaternion.identity);

        navInstance = NavMesh.AddNavMeshData(data);
    }

}
