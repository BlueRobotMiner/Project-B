using System.Collections.Generic;
using UnityEngine;

public struct MazeSpawnPlan
{
    public Vector2Int PlayerTile;
    public Vector2Int MonsterTile;
}

public static class MazeSpawns
{
    static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public static MazeSpawnPlan Plan(MazeGrid grid, Vector2Int playerTile, int seed, int minMonsterDistance)
    {
        int[,] dist = DistancesFrom(grid, playerTile);
        var rng = new System.Random(seed + 1); // separate random stream from the layout

        var candidates = new List<Vector2Int>();
        Vector2Int farthest = playerTile;
        int farthestDist = -1;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                if (grid.Walls[x, y] || dist[x, y] < 0)
                    continue;

                if (dist[x, y] > farthestDist)
                {
                    farthestDist = dist[x, y];
                    farthest = new Vector2Int(x, y);
                }

                if (dist[x, y] >= minMonsterDistance)
                    candidates.Add(new Vector2Int(x, y));
            }
        }

        // Small mazes may have no candidates, so fall back to the farthest tile
        Vector2Int monster = candidates.Count > 0
            ? candidates[rng.Next(candidates.Count)]
            : farthest;

        return new MazeSpawnPlan
        {
            PlayerTile = playerTile,
            MonsterTile = monster
        };
    }

    // Walking distance (in tiles) from start to every tile. -1 = unreachable.
    public static int[,] DistancesFrom(MazeGrid grid, Vector2Int start)
    {
        int[,] dist = new int[grid.Width, grid.Height];
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
                dist[x, y] = -1;

        var queue = new Queue<Vector2Int>();
        dist[start.x, start.y] = 0;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int d in Directions)
            {
                Vector2Int n = current + d;

                if (n.x < 0 || n.y < 0 || n.x >= grid.Width || n.y >= grid.Height)
                    continue;
                if (grid.Walls[n.x, n.y] || dist[n.x, n.y] != -1)
                    continue;

                dist[n.x, n.y] = dist[current.x, current.y] + 1;
                queue.Enqueue(n);
            }
        }

        return dist;
    }
}
