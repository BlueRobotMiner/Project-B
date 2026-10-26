using System.Collections.Generic;
using UnityEngine;

// Describes a room in grid tiles, so the generator knows what to leave alone.
public class RoomLayout
{
    public Vector2Int Center;
    public readonly HashSet<Vector2Int> Reserved = new HashSet<Vector2Int>();  // footprint + wall ring
    public readonly List<Vector2Int> Entrances = new List<Vector2Int>();       // possible openings
    public readonly List<Vector2Int> Chosen = new List<Vector2Int>();          // openings used this run

    public static RoomLayout Empty
    {
        get { return new RoomLayout(); }
    }

    // Fallback when no prefab is assigned: a plain square with one opening per side
    public static RoomLayout Square(Vector2Int center, int tilesAcross)
    {
        var layout = new RoomLayout { Center = center };
        int half = tilesAcross / 2;

        for (int dx = -half; dx <= half; dx++)
            for (int dy = -half; dy <= half; dy++)
                layout.Reserved.Add(center + new Vector2Int(dx, dy));

        layout.Entrances.Add(center + new Vector2Int(0, half));
        layout.Entrances.Add(center + new Vector2Int(0, -half));
        layout.Entrances.Add(center + new Vector2Int(half, 0));
        layout.Entrances.Add(center + new Vector2Int(-half, 0));
        return layout;
    }

    // Reads the room prefab: every child named "Entrance..." is a possible opening
    public static RoomLayout FromPrefab(GameObject prefab, Vector2Int center, float cellSize, int tilesAcross)
    {
        var layout = new RoomLayout { Center = center };
        int half = tilesAcross / 2;

        if (tilesAcross % 2 == 0)
            Debug.LogWarning("Room tilesAcross should be odd so the room has a center cell.");

        for (int dx = -half; dx <= half; dx++)
            for (int dy = -half; dy <= half; dy++)
                layout.Reserved.Add(center + new Vector2Int(dx, dy));

        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Entrance"))
                continue;

            float fx = t.localPosition.x / cellSize;
            float fz = t.localPosition.z / cellSize;
            int dx2 = Mathf.RoundToInt(fx);
            int dz2 = Mathf.RoundToInt(fz);

            if (Mathf.Abs(fx - dx2) > 0.1f || Mathf.Abs(fz - dz2) > 0.1f)
                Debug.LogWarning($"{t.name} in {prefab.name} is not on a tile center.");

            layout.Entrances.Add(center + new Vector2Int(dx2, dz2));
        }

        return layout;
    }

    // Randomly picks between minCount and maxCount openings for this run
    public void ChooseEntrances(System.Random rng, int minCount, int maxCount)
    {
        Chosen.Clear();
        var pool = new List<Vector2Int>(Entrances);
        int count = Mathf.Min(rng.Next(minCount, maxCount + 1), pool.Count);

        for (int i = 0; i < count; i++)
        {
            int index = rng.Next(pool.Count);
            Chosen.Add(pool[index]);
            pool.RemoveAt(index);
        }
    }
}
