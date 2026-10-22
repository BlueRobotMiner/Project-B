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
    static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 2), new Vector2Int(0, -2),
        new Vector2Int(2, 0), new Vector2Int(-2, 0)
    };

    public static MazeGrid Generate(int cellsX, int cellsY, int seed, List<RoomLayout> rooms, int hideoutCount)
    {
        int width = cellsX * 2 + 1;
        int height = cellsY * 2 + 1;
        var grid = new MazeGrid(width, height);
        var rng = new System.Random(seed);

        // 1. Mark each room's tiles as room tiles, and pick its openings for this run
        foreach (RoomLayout room in rooms)
        {
            room.ChooseEntrances(rng, 1, 2);

            foreach (Vector2Int t in room.Reserved)
            {
                if (!InBounds(t, width, height))
                    continue;
                grid.Room[t.x, t.y] = true;
                grid.Walls[t.x, t.y] = false;
            }
        }

        // 2. Open a path from each chosen opening outward to the nearest maze cell
        var stack = new Stack<Vector2Int>();
        foreach (RoomLayout room in rooms)
        {
            foreach (Vector2Int e in room.Chosen)
            {
                Vector2Int outward = FindOutward(e, room.Reserved, width, height);
                if (outward == Vector2Int.zero)
                    continue;

                grid.Walls[e.x, e.y] = false;
                Vector2Int p = e;
                while (!IsCell(p))
                {
                    p += outward;
                    if (!InBounds(p, width, height))
                        break;
                    grid.Walls[p.x, p.y] = false;
                }

                if (InBounds(p, width, height) && IsCell(p))
                    stack.Push(p);
            }
        }

        // Safety net: no openings at all still needs a starting cell
        if (stack.Count == 0)
        {
            grid.Walls[1, 1] = false;
            stack.Push(new Vector2Int(1, 1));
        }

        // 3. Recursive backtracker, growing out from the openings
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

        // 4. Hiding spots go on dead ends
        PlaceHideouts(grid, rng, hideoutCount);

        return grid;
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
        Vector2Int[] steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (Vector2Int s in steps)
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
        Vector2Int[] steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (Vector2Int s in steps)
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

            Vector2Int[] steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (Vector2Int s in steps)
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
        Vector2Int[] steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (Vector2Int s in steps)
        {
            Vector2Int n = p + s;
            if (InBounds(n, grid.Width, grid.Height) && !grid.Walls[n.x, n.y] && !grid.Room[n.x, n.y])
                count++;
        }
        return count;
    }

}
