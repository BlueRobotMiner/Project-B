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

    [Header("Safe Rooms")]
    [SerializeField] private GameObject[] requiredRoomPrefabs;   // A, B, C: each one is placed exactly once
    [SerializeField] private GameObject[] fillerRoomPrefabs;     // D, E: picked at random to fill the rest
    [SerializeField] private int safeRoomCount = 5;              // Total safe rooms (never fewer than the required ones)
    [SerializeField] private int roomMargin = 3;                 // Tiles kept clear between rooms
    [SerializeField] private float roomLift = 0.06f;   // Raises rooms just above the generated floor tiles


    public Vector3 PlayerSpawnWorld { get; private set; }
    public Vector3 MonsterSpawnWorld { get; private set; }

    private class SafeRoomPlan
    {
        public GameObject prefab;
        public RoomLayout layout;
    }

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
        Random.InitState(usedSeed);   // Same seed gives the same rooms and decor

        Vector2Int centerTile = new Vector2Int(2 * (cellsX / 2) + 1, 2 * (cellsY / 2) + 1);

        RoomLayout startRoom = startRoomPrefab != null
            ? RoomLayout.FromPrefab(startRoomPrefab, centerTile, cellSize, roomTilesAcross)
            : RoomLayout.Square(centerTile, roomTilesAcross);

        List<SafeRoomPlan> safeRooms = PlanSafeRooms(startRoom);

        var rooms = new List<RoomLayout> { startRoom };
        foreach (SafeRoomPlan plan in safeRooms)
            rooms.Add(plan.layout);

        grid = GenerateWithMinPath(usedSeed, rooms, centerTile, out usedSeed);

        MazeSpawnPlan spawnPlan = MazeSpawns.Plan(grid, centerTile, usedSeed, minMonsterDistance);
        PlayerSpawnWorld = TileToWorld(spawnPlan.PlayerTile);
        MonsterSpawnWorld = TileToWorld(spawnPlan.MonsterTile);

        SpawnWalls();
        SpawnFloor();
        SpawnRoom(startRoom, startRoomPrefab);
        foreach (SafeRoomPlan plan in safeRooms)
            SpawnSafeRoom(plan);
        SpawnHideouts();

        if (bakeNavMesh)
            BakeNavMesh();

        Debug.Log($"Maze built with seed {usedSeed}. Safe rooms: {safeRooms.Count}. Player {spawnPlan.PlayerTile}, monster {spawnPlan.MonsterTile}");
    }

    // ---- Planning: where the safe rooms go ----

    private List<SafeRoomPlan> PlanSafeRooms(RoomLayout startRoom)
    {
        var plans = new List<SafeRoomPlan>();
        var prefabs = new List<GameObject>();

        // Required rooms (A, B, C) come first, so they get space before the filler rooms
        if (requiredRoomPrefabs != null)
        {
            foreach (GameObject p in requiredRoomPrefabs)
                if (p != null) prefabs.Add(p);
        }

        // Filler rooms (D, E) fill the remaining slots at random
        int requiredCount = prefabs.Count;
        int fillerSlots = Mathf.Max(0, safeRoomCount - requiredCount);
        if (fillerRoomPrefabs != null && fillerRoomPrefabs.Length > 0)
        {
            for (int i = 0; i < fillerSlots; i++)
                prefabs.Add(fillerRoomPrefabs[Random.Range(0, fillerRoomPrefabs.Length)]);
        }

        int half = roomTilesAcross / 2 + roomMargin;
        var taken = new List<Vector2Int> { startRoom.Center };

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            Vector2Int? center = FindFreeCenter(half, taken);

            if (center == null)
            {
                string kind = i < requiredCount ? "REQUIRED" : "filler";
                Debug.LogError($"MazeBuilder: no space left for {kind} room {prefab.name}. Make the maze bigger or use fewer rooms.");
                continue;
            }

            taken.Add(center.Value);
            plans.Add(new SafeRoomPlan
            {
                prefab = prefab,
                layout = RoomLayout.FromPrefab(prefab, center.Value, cellSize, roomTilesAcross)
            });
        }

        return plans;
    }

    // Finds a room center on an odd tile (like the start room) that fits and doesn't overlap.
    private Vector2Int? FindFreeCenter(int half, List<Vector2Int> taken)
    {
        int width = 2 * cellsX + 1;
        int height = 2 * cellsY + 1;

        for (int attempt = 0; attempt < 200; attempt++)
        {
            Vector2Int c = new Vector2Int(2 * Random.Range(1, cellsX) + 1, 2 * Random.Range(1, cellsY) + 1);

            if (c.x - half < 1 || c.y - half < 1 || c.x + half > width - 2 || c.y + half > height - 2)
                continue;

            bool overlaps = false;
            foreach (Vector2Int t in taken)
            {
                if (Mathf.Abs(t.x - c.x) < 2 * half && Mathf.Abs(t.y - c.y) < 2 * half)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps) return c;
        }

        return null;
    }

    // ---- Generation ----

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

        // Floor under everything that isn't a wall, including room tiles,
        // so a room prefab with a smaller floor can't leave a hole
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
                if (!grid.Walls[x, y])
                    SpawnTile(floorTilePrefab, new Vector2Int(x, y), 0.1f, -0.05f);
    }


    private void SpawnHideouts()
    {
        if (hideoutPrefab == null)
            return;

        foreach (Vector2Int h in grid.Hideouts)
            SpawnTile(hideoutPrefab, h, 0.1f, 0.05f);
    }

    // Spawns a room prefab at its tile and opens the entrances the generator chose.
    // Spawns a room prefab at its tile and opens the entrances the generator chose.
    private GameObject SpawnRoom(RoomLayout room, GameObject prefab)
    {
        if (prefab == null)
            return null;

        GameObject roomObject = Instantiate(prefab, mazeRoot);

        // Lift the room slightly so its floor sits above the generated floor tiles
        roomObject.transform.localPosition = TileToLocal(room.Center) + new Vector3(0f, roomLift, 0f);

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

        return roomObject;
    }

    // A safe room: spawn it and roll its decor. Objectives are already inside A, B, and C.
    private void SpawnSafeRoom(SafeRoomPlan plan)
    {
        GameObject roomObject = SpawnRoom(plan.layout, plan.prefab);
        if (roomObject == null) return;

        foreach (DecorGroup group in roomObject.GetComponentsInChildren<DecorGroup>(true))
            group.Roll();
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
