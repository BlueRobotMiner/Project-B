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
    [SerializeField] private int roomTilesAcross = 15;   // Start room only
    [SerializeField] private float roomLift = 0.06f;     // Raises rooms just above the generated floor tiles

    [Header("Safe Rooms")]
    [Tooltip("Must match the safe room prefab's size in tiles (its Box Collider size divided by Cell Size). Odd number.")]
    [SerializeField] private int safeRoomTilesAcross = 11;
    [SerializeField] private GameObject[] requiredRoomPrefabs;   // A, B, C: each one is placed exactly once
    [SerializeField] private GameObject[] fillerRoomPrefabs;     // D, E: picked at random to fill the rest
    [SerializeField] private int safeRoomCount = 5;              // Total safe rooms (never fewer than the required ones)
    [SerializeField] private int roomMargin = 3;                 // Tiles kept clear between rooms

    [Header("NavMesh")]
    [SerializeField] private float navAgentRadius = 0.4f;        // Match the monster's NavMeshAgent Radius
    [SerializeField] private float navVoxelSize = 0.1f;          // Smaller = finer detail, so corners connect

    public Vector3 PlayerSpawnWorld { get; private set; }
    public Vector3 MonsterSpawnWorld { get; private set; }

    private class SafeRoomPlan
    {
        public GameObject prefab;
        public RoomLayout layout;
    }

    private MazeGrid grid;
    private NavMeshDataInstance navInstance;
    private readonly List<NavMeshDataInstance> roomNavInstances = new List<NavMeshDataInstance>();
    private readonly Dictionary<SafeRoom, List<Vector2Int>> roomInteriors = new Dictionary<SafeRoom, List<Vector2Int>>();
    private List<RoomLayout> bakedRooms = new List<RoomLayout>();   // Index 0 is the start room

    // The top surface of the generated floor tiles is at height 0
    private const float FloorTop = 0f;

    private void OnEnable()
    {
        SafeRoom.LightsOff += OpenRoomNavMesh;
    }

    private void OnDisable()
    {
        SafeRoom.LightsOff -= OpenRoomNavMesh;
    }

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
        bakedRooms = rooms;

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

        int half = safeRoomTilesAcross / 2 + roomMargin;
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
                layout = RoomLayout.FromPrefab(prefab, center.Value, cellSize, safeRoomTilesAcross)
            });
        }

        return plans;
    }

    // Finds a room center on an odd tile (like the start room) that fits and doesn't overlap.
// Tries many spots and keeps the one farthest from the rooms already placed,
// so the rooms spread across the maze instead of bunching together.
    private Vector2Int? FindFreeCenter(int half, List<Vector2Int> taken)
    {
        int width = 2 * cellsX + 1;
        int height = 2 * cellsY + 1;

        Vector2Int? best = null;
        int bestScore = -1;

        for (int attempt = 0; attempt < 300; attempt++)
        {
            Vector2Int c = new Vector2Int(2 * Random.Range(1, cellsX) + 1, 2 * Random.Range(1, cellsY) + 1);

            if (c.x - half < 1 || c.y - half < 1 || c.x + half > width - 2 || c.y + half > height - 2)
                continue;

            bool overlaps = false;
            int nearest = int.MaxValue;

            foreach (Vector2Int t in taken)
            {
                if (Mathf.Abs(t.x - c.x) < 2 * half && Mathf.Abs(t.y - c.y) < 2 * half)
                {
                    overlaps = true;
                    break;
                }

                int dist = Mathf.Abs(t.x - c.x) + Mathf.Abs(t.y - c.y);
                nearest = Mathf.Min(nearest, dist);
            }

            if (overlaps) continue;

            // Keep the spot that is farthest from its nearest neighbour
            if (nearest > bestScore)
            {
                bestScore = nearest;
                best = c;
            }
        }

        return best;
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
        foreach (NavMeshDataInstance instance in roomNavInstances)
            instance.Remove();
        roomNavInstances.Clear();
        roomInteriors.Clear();

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

    // Floor under every non-wall tile, including room tiles, so a room prefab can't leave a hole.
    private void SpawnFloor()
    {
        if (floorTilePrefab == null)
            return;

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

    // Spawns a room prefab at its tile, lifted slightly above the floor, and opens the chosen entrances.
    private GameObject SpawnRoom(RoomLayout room, GameObject prefab)
    {
        if (prefab == null)
            return null;

        GameObject roomObject = Instantiate(prefab, mazeRoot);
        roomObject.transform.localPosition = TileToLocal(room.Center) + new Vector3(0f, roomLift, 0f);

        // Entrance markers are wall copies. Hide the ones chosen as openings, keep the rest as walls.
        foreach (Transform t in roomObject.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Entrance"))
                continue;

            Vector2Int tile = room.Center + new Vector2Int(
                Mathf.RoundToInt(t.localPosition.x / cellSize),
                Mathf.RoundToInt(t.localPosition.z / cellSize));

            if (room.Chosen.Contains(room.Snap(tile)))
                t.gameObject.SetActive(false);
        }

        return roomObject;
    }

    // A safe room: spawn it, roll its decor, and remember its inside tiles so its NavMesh
    // can be added when its lights go off.
    private void SpawnSafeRoom(SafeRoomPlan plan)
    {
        GameObject roomObject = SpawnRoom(plan.layout, plan.prefab);
        if (roomObject == null) return;

        foreach (DecorGroup group in roomObject.GetComponentsInChildren<DecorGroup>(true))
            group.Roll();

        SafeRoom room = roomObject.GetComponent<SafeRoom>();
        if (room != null)
            roomInteriors[room] = InteriorTiles(plan.layout);
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

    // ---- NavMesh ----

    // Build settings: finer voxels so corners connect, and a smaller radius to match the monster.
    private NavMeshBuildSettings NavSettings()
    {
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = navAgentRadius;
        settings.overrideVoxelSize = true;
        settings.voxelSize = navVoxelSize;
        settings.minRegionArea = 0.01f;   // Keep small corner pieces
        return settings;
    }

    // The main NavMesh covers corridors, the start room, and the outside edge of each safe room
    // (the openings). A safe room's inside is left out until its lights go off.
    private void BakeNavMesh()
    {
        navInstance.Remove();

        var sources = new List<NavMeshBuildSource>();

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                Vector2Int tile = new Vector2Int(x, y);
                if (IsWalkable(tile))
                    sources.Add(TileSource(tile, FloorTop));
            }
        }

        float worldWidth = grid.Width * cellSize;
        float worldDepth = grid.Height * cellSize;
        Vector3 boundsCenter = mazeRoot.position + new Vector3(worldWidth * 0.5f, 0f, worldDepth * 0.5f);
        Bounds bounds = new Bounds(boundsCenter, new Vector3(worldWidth, 10f, worldDepth));

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(NavSettings(), sources, bounds, Vector3.zero, Quaternion.identity);

        navInstance = NavMesh.AddNavMeshData(data);
        Debug.Log($"MazeBuilder: NavMesh built from {sources.Count} walkable tiles");
    }

    // Called when a safe room's lights go off: add a NavMesh for just that room's inside,
    // at the room's floor height, so the monster can walk in.
    private void OpenRoomNavMesh(SafeRoom room)
    {
        if (!roomInteriors.TryGetValue(room, out List<Vector2Int> tiles))
            return;

        roomInteriors.Remove(room);   // Only open each room once

        var sources = new List<NavMeshBuildSource>();
        Vector2Int min = tiles.Count > 0 ? tiles[0] : Vector2Int.zero;
        Vector2Int max = min;

        foreach (Vector2Int tile in tiles)
        {
            min = Vector2Int.Min(min, tile);
            max = Vector2Int.Max(max, tile);

            if (!grid.Walls[tile.x, tile.y])
                sources.Add(TileSource(tile, roomLift));
        }

        if (sources.Count == 0) return;

        Vector3 a = TileToWorld(min);
        Vector3 b = TileToWorld(max);
        Vector3 mid = (a + b) * 0.5f;
        Vector3 size = new Vector3(Mathf.Abs(b.x - a.x) + cellSize * 2f, 10f, Mathf.Abs(b.z - a.z) + cellSize * 2f);
        Bounds bounds = new Bounds(mid, size);

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(NavSettings(), sources, bounds, Vector3.zero, Quaternion.identity);

        roomNavInstances.Add(NavMesh.AddNavMeshData(data));
        Debug.Log($"MazeBuilder: opened the NavMesh inside '{room.gameObject.name}' ({sources.Count} tiles)");
    }

    // A box whose top surface sits exactly at topY, so the NavMesh lies flat on the floor.
    private NavMeshBuildSource TileSource(Vector2Int tile, float topY)
    {
        const float thickness = 0.2f;
        Vector3 center = mazeRoot.position + TileToLocal(tile) + new Vector3(0f, topY - thickness * 0.5f, 0f);

        return new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box,
            size = new Vector3(cellSize, thickness, cellSize),
            transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
            area = 0
        };
    }

    // Tiles inside a room, not counting its outer wall ring.
    private List<Vector2Int> InteriorTiles(RoomLayout room)
    {
        var tiles = new List<Vector2Int>();
        int half = room.TilesAcross / 2;

        for (int dx = -half + 1; dx < half; dx++)
            for (int dy = -half + 1; dy < half; dy++)
                tiles.Add(room.Center + new Vector2Int(dx, dy));

        return tiles;
    }

    // Corridors and the start room are walkable. A safe room's inside is not (until its lights go off).
    // On a safe room's outer edge, only the chosen openings are walkable, so the walls stay solid.
    private bool IsWalkable(Vector2Int tile)
    {
        if (grid.Walls[tile.x, tile.y])
            return false;

        for (int i = 0; i < bakedRooms.Count; i++)
        {
            RoomLayout room = bakedRooms[i];
            int half = room.TilesAcross / 2;
            int dx = Mathf.Abs(tile.x - room.Center.x);
            int dy = Mathf.Abs(tile.y - room.Center.y);

            if (dx > half || dy > half)
                continue;   // Not in this room

            bool onEdge = dx == half || dy == half;
            if (!onEdge)
                return i == 0;   // Start room inside is walkable; safe room insides are not

            return room.Chosen.Contains(tile);   // Outer edge: only the openings
        }

        return true;   // Corridor
    }
}
