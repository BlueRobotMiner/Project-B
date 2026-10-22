using System.Collections.Generic;
using UnityEngine;

// Describes a start room in grid tiles, so the generator knows what to leave alone.
public class RoomLayout
{
    public readonly HashSet<Vector2Int> Reserved = new HashSet<Vector2Int>();
    public readonly List<Vector2Int> Entrances = new List<Vector2Int>();

    public static RoomLayout Empty
    {
        get { return new RoomLayout(); }
    }

    // Fallback when no prefab is assigned: a plain square with one opening per side
    public static RoomLayout Square(Vector2Int center, int tilesAcross)
    {
        var layout = new RoomLayout();
        int half = tilesAcross / 2; // 15 tiles -> half = 7

        for (int dx = -half; dx <= half; dx++)
            for (int dy = -half; dy <= half; dy++)
                layout.Reserved.Add(center + new Vector2Int(dx, dy));

        layout.Entrances.Add(center + new Vector2Int(0, half));
        layout.Entrances.Add(center + new Vector2Int(0, -half));
        layout.Entrances.Add(center + new Vector2Int(half, 0));
        layout.Entrances.Add(center + new Vector2Int(-half, 0));

        return layout;
    }

    // Reads the room prefab: every child named "Entrance..." becomes an opening
    public static RoomLayout FromPrefab(GameObject prefab, Vector2Int center, float cellSize, int tilesAcross)
    {
        var layout = new RoomLayout();
        int half = tilesAcross / 2;

        if (tilesAcross % 2 == 0)
            Debug.LogWarning("Room tilesAcross should be odd so the room has a center cell.");

        for (int dx = -half; dx <= half; dx++)
            for (int dy = -half; dy <= half; dy++)
                layout.Reserved.Add(center + new Vector2Int(dx, dy));

        // GetComponentsInChildren works on the prefab asset, so no instance is needed yet
        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Entrance"))
                continue;

            // Prefab root sits on the center tile, so local position maps directly to tiles
            float fx = t.localPosition.x / cellSize;
            float fz = t.localPosition.z / cellSize;
            int dx2 = Mathf.RoundToInt(fx);
            int dz2 = Mathf.RoundToInt(fz);

            if (Mathf.Abs(fx - dx2) > 0.1f || Mathf.Abs(fz - dz2) > 0.1f)
                Debug.LogWarning($"{t.name} in {prefab.name} is not on a tile center. Move it to a multiple of cellSize.");

            layout.Entrances.Add(center + new Vector2Int(dx2, dz2));
        }

        return layout;
    }
}
