using System.Text;
using UnityEngine;
using System.Collections.Generic;

public class MazeDebugPrinter : MonoBehaviour
{
    [SerializeField] private int cellsX = 10;
    [SerializeField] private int cellsY = 10;
    [SerializeField] private int seed = 12345;
    [SerializeField] private int startRoomRadius = 1;
    [SerializeField] private int minMonsterDistance = 30;

    private void Start()
    {
        MazeGrid grid = MazeGenerator.Generate(cellsX, cellsY, seed, new List<RoomLayout>(), 0);

        Vector2Int playerTile = new Vector2Int(2 * (cellsX / 2) + 1, 2 * (cellsY / 2) + 1);
        MazeSpawnPlan plan = MazeSpawns.Plan(grid, playerTile, seed, minMonsterDistance);

        var sb = new StringBuilder();
        // Print the top row first so the picture matches the Scene view
        for (int y = grid.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                Vector2Int tile = new Vector2Int(x, y);
                if (tile == plan.PlayerTile)
                    sb.Append('P');
                else if (tile == plan.MonsterTile)
                    sb.Append('M');
                else
                    sb.Append(grid.Walls[x, y] ? '#' : '.');
            }
            sb.AppendLine();
        }

        Debug.Log($"Maze seed {seed} ({grid.Width}x{grid.Height} grid)\n{sb}");
    }
}
