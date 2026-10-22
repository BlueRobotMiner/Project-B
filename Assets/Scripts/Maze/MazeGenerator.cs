using System.Collections.Generic;
using UnityEngine;

public class MazeGrid
{
    public readonly int Width;
    public readonly int Height;
    public readonly bool[,] Walls; // true = wall, false = floor

    public MazeGrid(int width, int height)
    {
        Width = width;
        Height = height;
        Walls = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                Walls[x, y] = true; // start fully solid
    }
}

public static class MazeGenerator
{
    // Moves of 2 tiles, because cells sit on odd coordinates
    static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 2), new Vector2Int(0, -2),
        new Vector2Int(2, 0), new Vector2Int(-2, 0)
    };

    public static MazeGrid Generate(int cellsX, int cellsY, int seed, RoomLayout room)
    {
        int width = cellsX * 2 + 1;
        int height = cellsY * 2 + 1;
        var grid = new MazeGrid(width, height);
        var rng = new System.Random(seed);

        // 1. Room tiles are open floor the maze never carves into
        foreach (Vector2Int t in room.Reserved)
            if (InBounds(t, width, height))
                grid.Walls[t.x, t.y] = false;

        // 2. From each entrance, open a path outward until we reach a maze cell
        var stack = new Stack<Vector2Int>();
        foreach (Vector2Int e in room.Entrances)
        {
            if (!InBounds(e, width, height))
                continue;

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

        // Safety net: an empty room still needs a starting cell
        if (stack.Count == 0)
        {
            grid.Walls[1, 1] = false;
            stack.Push(new Vector2Int(1, 1));
        }

        // 3. Recursive backtracker, growing out from every entrance
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

        return grid;
    }

    static bool IsCell(Vector2Int p)
    {
        return p.x % 2 == 1 && p.y % 2 == 1;
    }

    static bool InBounds(Vector2Int p, int width, int height)
    {
        return p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
    }

    // The direction from an entrance that leads out of the room
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
}
