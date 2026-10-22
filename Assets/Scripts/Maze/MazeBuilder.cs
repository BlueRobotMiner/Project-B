using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MazeBuilder : MonoBehaviour
{
    [SerializeField] private int cellsX = 10;
    [SerializeField] private int cellsY = 10;
    [SerializeField] private int seed = 0;
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private float wallHeight = 4f;
    [SerializeField] private GameObject wallPrefab;      // CornWall
    [SerializeField] private GameObject floorTilePrefab; // a flat cube
    [SerializeField] private GameObject hideoutPrefab;   // a trigger cube on the floor
    [SerializeField] private Transform mazeRoot;
    [SerializeField] private bool bakeNavMesh = true;
    [SerializeField] private bool buildOnStart = false;
    [SerializeField] private int minMonsterDistance = 30;
    [SerializeField] private int minExitDistance = 120;
    [SerializeField] private int maxAttempts = 50;
    [SerializeField] private int hideoutCount = 4;
    [SerializeField] private GameObject startRoomPrefab;
    [SerializeField] private int roomTilesAcross = 15;

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

        RoomLayout startRoom = startRoomPrefab != null
            ? RoomLayout.FromPrefab(startRoomPrefab, centerTile, cellSize, roomTilesAcross)
            : RoomLayout.Square(centerTile, roomTilesAcross);

        var rooms = new List<RoomLayout> { startRoom };

        grid = GenerateWithMinPath(usedSeed, rooms, centerTile, out usedSeed);

        MazeSpawnPlan plan = MazeSpawns.Plan(grid, centerTile, usedSeed, minMonsterDistance);
        PlayerSpawnWorld = TileToWorld(plan.PlayerTile);
        MonsterSpawnWorld = TileToWorld(plan.MonsterTile);

        SpawnWalls();
        SpawnFloor();
        SpawnRoom(startRoom);
        SpawnHideouts();

        if (bakeNavMesh)
            BakeNavMesh();

        Debug.Log($"Maze built with seed {usedSeed}. Player {plan.PlayerTile}, monster {plan.MonsterTile}");
    }

    private MazeGrid GenerateWithMinPath(int baseSeed, List<RoomLayout> rooms, Vector2Int centerTile, out int usedSeed)
    {
        MazeGrid result = null;
        usedSeed = baseSeed;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            usedSeed = baseSeed + attempt;
            result = MazeGenerator.Generate(cellsX, cellsY, usedSeed, rooms, hideoutCount);

            int[,] dist = MazeSpawns.DistancesFrom(result, centerTile);
            int farthest = 0;
            foreach (int d in dist)
                farthest = Mathf.Max(farthest, d);

           if (farthest >= minExitDistance && MazeGenerator.EntrancesHaveJunction(result, rooms, 6))
                break;

        }

        return result;
    }

    private void ClearOldMaze()
    {
        for (int i = mazeRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(mazeRoot.GetChild(i).gameObject);
    }

    private void SpawnWalls()
    {
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
                if (grid.Walls[x, y])
                    SpawnTile(wallPrefab, new Vector2Int(x, y), wallHeight, wallHeight * 0.5f);
    }

    private void SpawnFloor()
    {
        if (floorTilePrefab == null)
            return;

        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
                if (!grid.Walls[x, y] && !grid.Room[x, y])
                    SpawnTile(floorTilePrefab, new Vector2Int(x, y), 0.1f, -0.05f);
    }

    private void SpawnHideouts()
    {
        if (hideoutPrefab == null)
            return;

        foreach (Vector2Int h in grid.Hideouts)
            SpawnTile(hideoutPrefab, h, 0.1f, 0.05f);
    }

    private void SpawnRoom(RoomLayout room)
    {
        if (startRoomPrefab == null)
            return;

        GameObject roomObject = Instantiate(startRoomPrefab, mazeRoot);
        roomObject.transform.localPosition = TileToLocal(room.Center);

        // Entrance markers are wall copies. Hide the ones chosen as openings, keep the rest as walls.
        foreach (Transform t in roomObject.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Entrance"))
                continue;

            Vector2Int tile = room.Center + new Vector2Int(
                Mathf.RoundToInt(t.localPosition.x / cellSize),
                Mathf.RoundToInt(t.localPosition.z / cellSize));

            if (room.Chosen.Contains(tile))
                t.gameObject.SetActive(false);
        }
    }

    // Places one piece per tile. Scale and height are set per tile; the prefab should be a unit cube.
    private void SpawnTile(GameObject prefab, Vector2Int tile, float height, float yOffset)
    {
        GameObject piece = Instantiate(prefab, mazeRoot);
        piece.transform.localPosition = TileToLocal(tile) + new Vector3(0f, yOffset, 0f);
        piece.transform.localScale = new Vector3(cellSize, height, cellSize);
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
        NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.RenderMeshes, 0, new List<NavMeshBuildMarkup>(), sources);

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            NavMesh.GetSettingsByID(0), sources, bounds, Vector3.zero, Quaternion.identity);

        navInstance = NavMesh.AddNavMeshData(data);
    }
}
