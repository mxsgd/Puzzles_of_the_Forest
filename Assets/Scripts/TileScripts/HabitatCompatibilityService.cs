/// <summary>
/// Symmetric "kinship" relation between habitat animals. Originally gated whether two habitats
/// could share a tile; tiles now hold at most one habitat each (see
/// <see cref="TileRuntimeStore.MaxHabitatsPerTile"/>), so this table is currently unused by any
/// gameplay system. Kept as reusable design data for a future perk pass (e.g. an animal
/// specialization perk that boosts one animal and penalizes ones it's marked incompatible with
/// here) rather than re-deriving these relations from scratch later.
/// </summary>
public static class HabitatCompatibilityService
{
    // Index order must match HabitatAnimal: Deer, Beaver, Bear, Bees.
    private static readonly int[,] Matrix =
    {
        // Deer, Beaver, Bear, Bees
        { 0, 1, 0, 1 }, // Deer
        { 1, 0, 0, 1 }, // Beaver
        { 0, 0, 0, 0 }, // Bear
        { 1, 1, 0, 0 }, // Bees
    };

    /// <summary>1 = compatible; 0 = incompatible.</summary>
    public static int GetCompatibility(HabitatAnimal a, HabitatAnimal b)
    {
        if (a == HabitatAnimal.None || b == HabitatAnimal.None) return 1;
        int ia = ToIndex(a);
        int ib = ToIndex(b);
        if (ia < 0 || ib < 0) return 0;
        return Matrix[ia, ib];
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
