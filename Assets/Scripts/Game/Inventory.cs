// Inventory
// Holds the item IDs the player has picked up (for example "Camera", "TailRotor", "Fuel", "Keys").
// Cleared when a run restarts or the player returns to the main menu.
using System.Collections.Generic;

public static class Inventory
{
    private static readonly HashSet<string> items = new HashSet<string>();

    public static void Add(string id)
    {
        items.Add(id);
    }

    public static bool Has(string id)
    {
        return items.Contains(id);
    }

    public static void Clear()
    {
        items.Clear();
    }
}
