using System.Collections.Generic;
using Tile = TileGrid.Tile;

/// <summary>
/// Walidacja „rdzenia” habitatu — odrzuca cienkie wężykowate regiony.
/// </summary>
public static class HabitatCoreValidation
{
    public const int DefaultMinNeighborsForCore = 3;
    public const int DefaultRequiredCoreTiles = 2;

    public static int GetMinNeighborsForCore(HabitatAnimal animal, HabitatRulesProfile rules) =>
        rules != null ? rules.GetMinNeighborsForCore(animal) : DefaultMinNeighborsForCore;

    public static int GetRequiredCoreTiles(HabitatAnimal animal, HabitatRulesProfile rules) =>
        rules != null ? rules.GetRequiredCoreTiles(animal) : DefaultRequiredCoreTiles;

    /// <summary>
    /// Kafel rdzeniowy: co najmniej <paramref name="minNeighborsForCore"/> sąsiadów z tego samego regionu.
    /// </summary>
    public static bool IsCoreTile(Tile tile, IReadOnlyList<Tile> regionTiles, int minNeighborsForCore)
    {
        if (tile == null || regionTiles == null || regionTiles.Count == 0)
            return false;

        int inRegion = 0;
        foreach (Tile n in HabitatRegionEnumerator.GetNeighborsSafe(tile))
        {
            if (n != null && RegionContains(regionTiles, n))
                inRegion++;
            if (inRegion >= minNeighborsForCore)
                return true;
        }
        return false;
    }

    public static int CountCoreTiles(IReadOnlyList<Tile> regionTiles, int minNeighborsForCore)
    {
        if (regionTiles == null || regionTiles.Count == 0)
            return 0;

        int coreCount = 0;
        for (int i = 0; i < regionTiles.Count; i++)
        {
            if (IsCoreTile(regionTiles[i], regionTiles, minNeighborsForCore))
                coreCount++;
        }
        return coreCount;
    }

    /// <summary>
    /// Habitat przechodzi walidację tylko gdy ma wystarczającą liczbę kafli rdzeniowych.
    /// </summary>
    public static bool ValidateCoreRequirement(
        IReadOnlyList<Tile> regionTiles,
        HabitatAnimal animal,
        HabitatRulesProfile rules,
        out int coreTileCount)
    {
        int minNeighbors = GetMinNeighborsForCore(animal, rules);
        int requiredCore = GetRequiredCoreTiles(animal, rules);
        coreTileCount = CountCoreTiles(regionTiles, minNeighbors);
        return coreTileCount >= requiredCore;
    }

    private static bool RegionContains(IReadOnlyList<Tile> regionTiles, Tile tile)
    {
        for (int i = 0; i < regionTiles.Count; i++)
        {
            if (ReferenceEquals(regionTiles[i], tile))
                return true;
        }
        return false;
    }
}
