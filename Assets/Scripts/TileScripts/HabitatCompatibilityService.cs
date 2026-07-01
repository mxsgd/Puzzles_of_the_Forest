/// <summary>
/// Symmetric compatibility between habitat animals for stacking / overlap.
/// Values are only 0 or 1 per design matrix.
/// </summary>
public static class HabitatCompatibilityService
{
    // Index order must match HabitatAnimal: Deer=1..5 for non-None
    private static readonly int[,] Matrix =
    {
        // Deer, Beaver, Bear, Bees, RockDweller
        { 0, 1, 0, 1, 1 }, // Deer
        { 1, 0, 0, 1, 0 }, // Beaver
        { 0, 0, 0, 0, 1 }, // Bear
        { 1, 1, 0, 0, 0 }
    };

    /// <summary>1 = compatible; 0 = incompatible (new animal cannot use tile biome contribution).</summary>
    public static int GetCompatibility(HabitatAnimal a, HabitatAnimal b)
    {
        if (a == HabitatAnimal.None || b == HabitatAnimal.None) return 1;
        int ia = ToIndex(a);
        int ib = ToIndex(b);
        if (ia < 0 || ib < 0) return 0;
        return Matrix[ia, ib];
    }

    /// <summary>True if newAnimal is compatible with the existing habitat animal on the tile (max 1 per tile).</summary>
    public static bool IsCompatibleWithAllOnTile(HabitatAnimal newAnimal, TileRuntimeStore store, TileGrid.Tile tile)
    {
        var r = store.Get(tile);
        if (r == null || r.habitatId < 0) return true;
        if (!store.TryGetHabitatAnimal(r.habitatId, out var existing) || existing == HabitatAnimal.None)
            return true;
        return GetCompatibility(newAnimal, existing) != 0;
    }

    private static int ToIndex(HabitatAnimal a)
    {
        return a switch
        {
            HabitatAnimal.Deer => 0,
            HabitatAnimal.Beaver => 1,
            HabitatAnimal.Bear => 2,
            HabitatAnimal.Bees => 3,
            _ => -1
        };
    }
}
