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
    [SerializeField] private bool buildOnStart = false;  // the spawner builds it instead
    [SerializeField] private int minMonsterDistance = 30; // in tiles
    [SerializeField] private GameObject startRoomPrefab; // Starter area prefab
    [SerializeField] private int roomTilesAcross = 15;   // must be odd
    [SerializeField] private int minExitDistance = 120; // minimum walk from start to farthest tile
    [SerializeField] private int maxAttempts = 50;      // how many seeds to try before giving up


    public Vector3 PlayerSpawnWorld { get; private set; }
    public Vector3 MonsterSpawnWorld { get; private set; }

    private MazeGrid grid;
    private NavMeshDataInstance navInstance;

    private void Start()
    {
        if (buildOnStart)
            Build();
    }

    public void Build()
    {
        ClearOldMaze();

        int usedSeed = seed == 0 ? Random.Range(1, int.MaxValue) : seed;
        Vector2Int centerTile = new Vector2Int(2 * (cellsX / 2) + 1, 2 * (cellsY / 2) + 1);

        // 1. Work out which tiles the start room owns and where its exits are
        RoomLayout room = startRoomPrefab != null
            ? RoomLayout.FromPrefab(startRoomPrefab, centerTile, cellSize, roomTilesAcross)
            : RoomLayout.Square(centerTile, roomTilesAcross);

        // 2. Generate the maze around the room
        grid = GenerateWithMinPath(usedSeed, room, centerTile, out usedSeed);


        // 3. Pick spawns
        MazeSpawnPlan plan = MazeSpawns.Plan(grid, centerTile, usedSeed, minMonsterDistance);
        PlayerSpawnWorld = TileToWorld(plan.PlayerTile);
        MonsterSpawnWorld = TileToWorld(plan.MonsterTile);

        // 4. Spawn geometry
        SpawnWalls();
        SpawnRoom(centerTile);

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

    private void SpawnRoom(Vector2Int centerTile)
    {
        if (startRoomPrefab == null)
            return;

        GameObject room = Instantiate(startRoomPrefab, mazeRoot);
        room.transform.localPosition = TileToLocal(centerTile);

        // Entrance markers are references only. Hide them so they don't block the openings.
        foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Entrance"))
                t.gameObject.SetActive(false);
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
        navInstance.Remove();

        float worldWidth = grid.Width * cellSize;
        float worldDepth = grid.Height * cellSize;
        Vector3 center = mazeRoot.position + new Vector3(worldWidth * 0.5f, 0f, worldDepth * 0.5f);
        Bounds bounds = new Bounds(center, new Vector3(worldWidth, 10f, worldDepth));

        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(
            bounds,
            ~0,
            NavMeshCollectGeometry.RenderMeshes,
            0,
            new List<NavMeshBuildMarkup>(),
            sources);

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            NavMesh.GetSettingsByID(0),
            sources,
            bounds,
            Vector3.zero,
            Quaternion.identity);

        navInstance = NavMesh.AddNavMeshData(data);
    }
        private MazeGrid GenerateWithMinPath(int baseSeed, RoomLayout room, Vector2Int centerTile, out int usedSeed)
    {
        MazeGrid result = null;
        usedSeed = baseSeed;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            usedSeed = baseSeed + attempt;
            result = MazeGenerator.Generate(cellsX, cellsY, usedSeed, room);

            // Longest walk from the start room to any floor tile
            int[,] dist = MazeSpawns.DistancesFrom(result, centerTile);
            int farthest = 0;
            foreach (int d in dist)
                farthest = Mathf.Max(farthest, d);

            if (farthest >= minExitDistance)
                break;
        }

        return result;
    }

}
