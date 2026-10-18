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

    public static MazeGrid Generate(int cellsX, int cellsY, int seed, int startRoomRadius)
    {
        int width = cellsX * 2 + 1;
        int height = cellsY * 2 + 1;
        var grid = new MazeGrid(width, height);
        var rng = new System.Random(seed);

        // Center cell, in cell coordinates
        int cx = cellsX / 2;
        int cy = cellsY / 2;

        // Keep the room inside the maze
        int r = Mathf.Min(startRoomRadius, cx, cellsX - 1 - cx, cy, cellsY - 1 - cy);

        // Grid tiles covered by the room (cells plus the gaps between them)
        int minX = 2 * (cx - r) + 1;
        int maxX = 2 * (cx + r) + 1;
        int minY = 2 * (cy - r) + 1;
        int maxY = 2 * (cy + r) + 1;

        var stack = new Stack<Vector2Int>();

        // Clear the start room and start the walk from every cell inside it
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                grid.Walls[x, y] = false;

                bool isCell = (x % 2 == 1) && (y % 2 == 1);
                if (isCell)
                    stack.Push(new Vector2Int(x, y));
            }
        }

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();

            // Directions where the cell two steps away is still a wall (unvisited)
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
                stack.Pop(); // dead end: go back
                continue;
            }

            Vector2Int dir = options[rng.Next(options.Count)];
            Vector2Int next = current + dir;

            // Knock out the wall between the two cells, then the new cell
            grid.Walls[current.x + dir.x / 2, current.y + dir.y / 2] = false;
            grid.Walls[next.x, next.y] = false;

            stack.Push(next);
        }

        return grid;
    }
}
