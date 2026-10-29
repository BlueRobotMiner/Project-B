using System.Collections.Generic;
using UnityEngine;

public class MazeGrid
{
    public readonly int Width;
    public readonly int Height;
    public readonly bool[,] Walls;  // true = wall
    public readonly bool[,] Room;   // true = a room owns this tile (its prefab provides the geometry)
    public readonly HashSet<Vector2Int> Hideouts = new HashSet<Vector2Int>();

    public MazeGrid(int width, int height)
    {
        Width = width;
        Height = height;
        Walls = new bool[width, height];
        Room = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                Walls[x, y] = true;
    }
}

public static class MazeGenerator
{
    // Chance that a dead end gets a second route, so the maze has loops
    private const float LoopChance = 0.8f;

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 2), new Vector2Int(0, -2),
        new Vector2Int(2, 0), new Vector2Int(-2, 0)
    };

    private static readonly Vector2Int[] Steps =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public static MazeGrid Generate(int cellsX, int cellsY, int seed, List<RoomLayout> rooms, int hideoutCount)
    {
        int width = cellsX * 2 + 1;
        int height = cellsY * 2 + 1;
        var grid = new MazeGrid(width, height);
        var rng = new System.Random(seed);

        // 1. Mark each room's footprint, and pick its openings for this run.
        //    The starter room is first in the list, so its openings are the main road.
        foreach (RoomLayout room in rooms)
        {
            room.ChooseEntrances(rng, 2, 3); // 2-3 entrances per room

            foreach (Vector2Int t in room.Reserved)
            {
                if (!InBounds(t, width, height))
                    continue;
                grid.Room[t.x, t.y] = true;
                grid.Walls[t.x, t.y] = false;
            }
        }
        foreach (RoomLayout room in rooms)
        {
            MarkRing(grid, room, width, height);
        }

        // 2. Carve each opening out to the nearest maze cell. Those cells seed the maze.
        var stack = new Stack<Vector2Int>();
        foreach (RoomLayout room in rooms)
        {
            foreach (Vector2Int e in room.Chosen)
            {
                if (CarveOpening(grid, e, room.Reserved, width, height, out Vector2Int cell))
                    stack.Push(cell);
            }
        }

        // Safety net: no openings at all still needs a starting cell
        if (stack.Count == 0)
        {
            grid.Walls[1, 1] = false;
            stack.Push(new Vector2Int(1, 1));
        }

        // 3. Grow the maze from those cells (recursive backtracker)
        Grow(grid, stack, rng, width, height);

        // 4. Connect everything to the starter room's road, so every room can be reached
        ConnectEverything(grid, rooms, width, height);

        // 5. Open some dead ends into loops, so there's more than one way around
        AddLoops(grid, rng, width, height, LoopChance);

        // 6. Hiding spots go on dead ends
        PlaceHideouts(grid, rng, hideoutCount);

        return grid;
    }

    // Carves from an opening outward to the nearest maze cell. Returns false if it can't.
    private static bool CarveOpening(MazeGrid grid, Vector2Int e, HashSet<Vector2Int> reserved, int width, int height, out Vector2Int cell)
    {
        cell = Vector2Int.zero;

        Vector2Int outward = FindOutward(e, reserved, width, height);
        if (outward == Vector2Int.zero)
            return false;

        grid.Walls[e.x, e.y] = false;
        Vector2Int p = e;
        while (!IsCell(p))
        {
            p += outward;
            if (!InBounds(p, width, height))
                return false;
            grid.Walls[p.x, p.y] = false;
        }

        cell = p;
        return true;
    }

    private static void Grow(MazeGrid grid, Stack<Vector2Int> stack, System.Random rng, int width, int height)
    {
        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();

            var options = new List<Vector2Int>();
            foreach (Vector2Int d in Directions)
            {
                Vector2Int n = current + d;
                bool inside = n.x > 0 && n.y > 0 && n.x < width - 1 && n.y < height - 1;
                if (inside && grid.Walls[n.x, n.y])
                    options.Add(d);
            }

            if (options.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int dir = options[rng.Next(options.Count)];
            Vector2Int next = current + dir;

            grid.Walls[current.x + dir.x / 2, current.y + dir.y / 2] = false;
            grid.Walls[next.x, next.y] = false;
            stack.Push(next);
        }
    }

    // Makes sure every opening and every open area connects back to the starter room's road.
    private static void ConnectEverything(MazeGrid grid, List<RoomLayout> rooms, int width, int height)
    {
        Vector2Int road = FindRoadStart(grid, rooms, width, height);
        HashSet<Vector2Int> main = Flood(grid, road, width, height);

        // Every room opening must reach the road
        foreach (RoomLayout room in rooms)
        {
            foreach (Vector2Int e in room.Chosen)
            {
                if (!main.Contains(e))
                {
                    ConnectToMain(grid, e, main, width, height);
                    main = Flood(grid, road, width, height);
                }
            }
        }

        // Any corridor that got cut off from the road
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                Vector2Int t = new Vector2Int(x, y);
                if (grid.Walls[x, y] || grid.Room[x, y] || main.Contains(t))
                    continue;

                ConnectToMain(grid, t, main, width, height);
                main = Flood(grid, road, width, height);
            }
        }
    }

    // The starter room's first opening, or a corner cell if it has none.
    private static Vector2Int FindRoadStart(MazeGrid grid, List<RoomLayout> rooms, int width, int height)
    {
        if (rooms.Count > 0 && rooms[0].Chosen.Count > 0)
            return rooms[0].Chosen[0];

        return new Vector2Int(1, 1);
    }

    // Breadth-first search from a tile to the road, carving the path it takes.
    // It never crosses a room's footprint, so rooms aren't tunnelled through.
    private static void ConnectToMain(MazeGrid grid, Vector2Int start, HashSet<Vector2Int> main, int width, int height)
    {
        if (main.Contains(start)) return;

        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var queue = new Queue<Vector2Int>();
        parent[start] = start;
        queue.Enqueue(start);

        Vector2Int found = new Vector2Int(-1, -1);

        while (queue.Count > 0 && found.x < 0)
        {
            Vector2Int p = queue.Dequeue();

            foreach (Vector2Int s in Steps)
            {
                Vector2Int n = p + s;
                if (n.x <= 0 || n.y <= 0 || n.x >= width - 1 || n.y >= height - 1) continue;
                if (grid.Room[n.x, n.y]) continue;
                if (parent.ContainsKey(n)) continue;

                parent[n] = p;

                if (main.Contains(n))
                {
                    found = n;
                    break;
                }

                queue.Enqueue(n);
            }
        }

        if (found.x < 0) return;

        // Carve back from the road to the start
        Vector2Int cur = found;
        while (true)
        {
            grid.Walls[cur.x, cur.y] = false;
            if (cur == start) break;
            cur = parent[cur];
        }
    }

    // Every open tile reachable from a starting tile.
    private static HashSet<Vector2Int> Flood(MazeGrid grid, Vector2Int start, int width, int height)
    {
        var seen = new HashSet<Vector2Int>();
        if (!InBounds(start, width, height) || grid.Walls[start.x, start.y])
            return seen;

        var queue = new Queue<Vector2Int>();
        seen.Add(start);
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();

            foreach (Vector2Int s in Steps)
            {
                Vector2Int n = p + s;
                if (!InBounds(n, width, height)) continue;
                if (grid.Walls[n.x, n.y]) continue;
                if (seen.Contains(n)) continue;

                seen.Add(n);
                queue.Enqueue(n);
            }
        }

        return seen;
    }

    // Turns some dead ends into loops by knocking out one wall toward a neighbouring open cell.
  // Turns dead ends into loops by knocking out a wall toward another open cell.
// Runs a few passes, since each knocked-out wall can leave new dead ends.
    private static void AddLoops(MazeGrid grid, System.Random rng, int width, int height, float chance)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            for (int x = 1; x < width - 1; x += 2)
            {
                for (int y = 1; y < height - 1; y += 2)
                {
                    Vector2Int p = new Vector2Int(x, y);
                    if (grid.Walls[x, y] || grid.Room[x, y]) continue;
                    if (OpenNeighbours(grid, p) != 1) continue;
                    if (rng.NextDouble() > chance) continue;

                    // Try the four directions in a random order
                    int start = rng.Next(Steps.Length);
                    for (int i = 0; i < Steps.Length; i++)
                    {
                        Vector2Int s = Steps[(start + i) % Steps.Length];
                        Vector2Int wall = p + s;
                        Vector2Int cell = p + s * 2;

                        if (!InBounds(cell, width, height) || !InBounds(wall, width, height)) continue;
                        if (wall.x <= 0 || wall.y <= 0 || wall.x >= width - 1 || wall.y >= height - 1) continue;
                        if (!grid.Walls[wall.x, wall.y] || grid.Room[wall.x, wall.y]) continue;
                        if (grid.Walls[cell.x, cell.y] || grid.Room[cell.x, cell.y]) continue;

                        grid.Walls[wall.x, wall.y] = false;
                        break;
                    }
                }
            }
        }
    }


    static void PlaceHideouts(MazeGrid grid, System.Random rng, int count)
    {
        var deadEnds = new List<Vector2Int>();
        for (int x = 1; x < grid.Width; x += 2)
        {
            for (int y = 1; y < grid.Height; y += 2)
            {
                Vector2Int p = new Vector2Int(x, y);
                if (grid.Walls[x, y] || grid.Room[x, y])
                    continue;
                if (OpenNeighbours(grid, p) == 1)
                    deadEnds.Add(p);
            }
        }

        for (int i = 0; i < count && deadEnds.Count > 0; i++)
        {
            int index = rng.Next(deadEnds.Count);
            grid.Hideouts.Add(deadEnds[index]);
            deadEnds.RemoveAt(index);
        }
    }

    static int OpenNeighbours(MazeGrid grid, Vector2Int p)
    {
        int count = 0;
        foreach (Vector2Int s in Steps)
        {
            Vector2Int n = p + s;
            if (InBounds(n, grid.Width, grid.Height) && !grid.Walls[n.x, n.y])
                count++;
        }
        return count;
    }

    static bool IsCell(Vector2Int p)
    {
        return p.x % 2 == 1 && p.y % 2 == 1;
    }

    static bool InBounds(Vector2Int p, int width, int height)
    {
        return p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
    }

    static Vector2Int FindOutward(Vector2Int e, HashSet<Vector2Int> reserved, int width, int height)
    {
        foreach (Vector2Int s in Steps)
        {
            Vector2Int n = e + s;
            if (InBounds(n, width, height) && !reserved.Contains(n))
                return s;
        }
        return Vector2Int.zero;
    }

    // True when every chosen entrance has a junction within maxDepth tiles, so it doesn't open into a dead end
    public static bool EntrancesHaveJunction(MazeGrid grid, List<RoomLayout> rooms, int maxDepth)
    {
        foreach (RoomLayout room in rooms)
        {
            foreach (Vector2Int e in room.Chosen)
            {
                if (!FindJunctionNear(grid, e, room.Reserved, maxDepth))
                    return false;
            }
        }
        return true;
    }

    static bool FindJunctionNear(MazeGrid grid, Vector2Int start, HashSet<Vector2Int> roomTiles, int maxDepth)
    {
        var dist = new Dictionary<Vector2Int, int>();
        var queue = new Queue<Vector2Int>();
        dist[start] = 0;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();

            // A junction is a maze cell with three or more open sides, found away from the entrance itself
            if (dist[p] > 0 && IsCell(p) && CorridorNeighbours(grid, p) >= 3)
                return true;

            if (dist[p] >= maxDepth)
                continue;

            foreach (Vector2Int s in Steps)
            {
                Vector2Int n = p + s;
                if (!InBounds(n, grid.Width, grid.Height)) continue;
                if (grid.Walls[n.x, n.y] || grid.Room[n.x, n.y]) continue;
                if (dist.ContainsKey(n)) continue;

                dist[n] = dist[p] + 1;
                queue.Enqueue(n);
            }
        }

        return false;
    }

    // Open sides that lead into the corridor system, not into a room
    static int CorridorNeighbours(MazeGrid grid, Vector2Int p)
    {
        int count = 0;
        foreach (Vector2Int s in Steps)
        {
            Vector2Int n = p + s;
            if (InBounds(n, grid.Width, grid.Height) && !grid.Walls[n.x, n.y] && !grid.Room[n.x, n.y])
                count++;
        }
        return count;
    }
    // Keeps a one-tile ring around a room open, so maze walls never sit against the room's walls.
    private static void MarkRing(MazeGrid grid, RoomLayout room, int width, int height)
    {
        int half = room.TilesAcross / 2 + 1;

        for (int dx = -half; dx <= half; dx++)
        {
            for (int dy = -half; dy <= half; dy++)
            {
                bool ring = Mathf.Abs(dx) == half || Mathf.Abs(dy) == half;
                if (!ring) continue;

                Vector2Int t = room.Center + new Vector2Int(dx, dy);
                if (!InBounds(t, width, height)) continue;
                if (t.x <= 0 || t.y <= 0 || t.x >= width - 1 || t.y >= height - 1) continue;
                if (grid.Room[t.x, t.y]) continue;

                grid.Walls[t.x, t.y] = false;
            }
        }
    }

}
