using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Walidacja „rdzenia” habitatu — odrzuca cienkie wężykowate regiony.
/// Przy enumeracji regionów: jedna <see cref="PrepareRegionCoreAnalysis"/> na region,
/// potem O(1) sprawdzenie per zwierzę.
/// </summary>
public static class HabitatCoreValidation
{
    public const int DefaultMinNeighborsForCore = 3;
    public const int DefaultRequiredCoreTiles = 2;
    private const int MaxMinNeighborsIndex = 6;

    public static int GetMinNeighborsForCore(HabitatAnimal animal, HabitatRulesProfile rules) =>
        rules != null ? rules.GetMinNeighborsForCore(animal) : DefaultMinNeighborsForCore;

    public static int GetRequiredCoreTiles(HabitatAnimal animal, HabitatRulesProfile rules) =>
        rules != null ? rules.GetRequiredCoreTiles(animal) : DefaultRequiredCoreTiles;

    /// <summary>
    /// Buduje HashSet regionu, liczby sąsiadów w regionie i tablicę progów core — raz na region DFS.
    /// </summary>
    public static void PrepareRegionCoreAnalysis(IReadOnlyList<Tile> regionTiles, HabitatRegionScratch scratch)
    {
        if (regionTiles == null || regionTiles.Count == 0 || scratch == null)
            return;

        int hash = ComputeRegionHash(regionTiles);
        if (scratch.CoreAnalysisHash == hash && scratch.NeighborsInRegion.Count > 0)
            return;

        scratch.CoreAnalysisHash = hash;
        scratch.RegionSet.Clear();
        scratch.NeighborsInRegion.Clear();

        for (int i = 0; i < regionTiles.Count; i++)
        {
            var t = regionTiles[i];
            if (t != null)
                scratch.RegionSet.Add(t);
        }

        scratch.CoreCentroid = Vector3.zero;
        int centroidCount = 0;

        for (int i = 0; i < regionTiles.Count; i++)
        {
            var t = regionTiles[i];
            if (t == null) continue;

            scratch.CoreCentroid += t.worldPos;
            centroidCount++;

            int inRegion = 0;
            foreach (Tile n in HabitatRegionEnumerator.GetNeighborsSafe(t))
            {
                if (n != null && scratch.RegionSet.Contains(n))
                    inRegion++;
            }

            scratch.NeighborsInRegion[t] = inRegion;
        }

        if (centroidCount > 0)
            scratch.CoreCentroid /= centroidCount;

        for (int minN = 0; minN <= MaxMinNeighborsIndex; minN++)
        {
            int coreCount = 0;
            for (int i = 0; i < regionTiles.Count; i++)
            {
                var t = regionTiles[i];
                if (t != null
                    && scratch.NeighborsInRegion.TryGetValue(t, out int neighbors)
                    && neighbors >= minN)
                    coreCount++;
            }

            scratch.CoreCountByMinThreshold[minN] = coreCount;
        }
    }

    public static int CountCoreTilesForMinNeighbors(HabitatRegionScratch scratch, int minNeighborsForCore)
    {
        if (scratch == null) return 0;
        int idx = Mathf.Clamp(minNeighborsForCore, 0, MaxMinNeighborsIndex);
        return scratch.CoreCountByMinThreshold[idx];
    }

    /// <summary>Kafel rdzeniowy — używa wcześniejszej analizy regionu.</summary>
    public static bool IsCoreTile(Tile tile, HabitatRegionScratch scratch, int minNeighborsForCore)
    {
        if (tile == null || scratch == null)
            return false;

        return scratch.NeighborsInRegion.TryGetValue(tile, out int neighbors)
               && neighbors >= minNeighborsForCore;
    }

    public static bool ValidateCoreRequirement(
        IReadOnlyList<Tile> regionTiles,
        HabitatAnimal animal,
        HabitatRulesProfile rules,
        HabitatRegionScratch scratch,
        out int coreTileCount,
        TileRuntimeStore store = null)
    {
        coreTileCount = 0;
        if (regionTiles == null || regionTiles.Count == 0)
            return false;

        PrepareRegionCoreAnalysis(regionTiles, scratch);

        int minNeighbors = GetMinNeighborsForCore(animal, rules);
        int requiredCore = GetRequiredCoreTiles(animal, rules);
        coreTileCount = CountLandCoreTiles(scratch, minNeighbors, regionTiles, store);
        return coreTileCount >= requiredCore;
    }

    /// <summary>Rdzeń habitatu — tylko suche kafle (woda nie liczy się jako rdzeń do spawnu).</summary>
    public static int CountLandCoreTiles(
        HabitatRegionScratch scratch,
        int minNeighborsForCore,
        IReadOnlyList<Tile> regionTiles,
        TileRuntimeStore store)
    {
        if (scratch == null || regionTiles == null)
            return 0;

        int count = 0;
        for (int i = 0; i < regionTiles.Count; i++)
        {
            var t = regionTiles[i];
            if (t == null || IsWaterTile(store, t))
                continue;
            if (IsCoreTile(t, scratch, minNeighborsForCore))
                count++;
        }

        return count;
    }

    /// <summary>Fallback bez scratch — tylko pojedyncze wywołania poza hot path.</summary>
    public static bool ValidateCoreRequirement(
        IReadOnlyList<Tile> regionTiles,
        HabitatAnimal animal,
        HabitatRulesProfile rules,
        out int coreTileCount,
        TileRuntimeStore store = null)
    {
        coreTileCount = 0;
        if (regionTiles == null || regionTiles.Count == 0)
            return false;

        int minNeighbors = GetMinNeighborsForCore(animal, rules);
        int requiredCore = GetRequiredCoreTiles(animal, rules);
        coreTileCount = CountCoreTilesSlow(regionTiles, minNeighbors, store);
        return coreTileCount >= requiredCore;
    }

    public static bool TryGetPrimaryCoreTile(
        IReadOnlyList<Tile> regionTiles,
        HabitatAnimal animal,
        HabitatRulesProfile rules,
        HabitatRegionScratch scratch,
        out Tile primaryCore,
        TileRuntimeStore store = null)
    {
        primaryCore = null;
        if (regionTiles == null || regionTiles.Count == 0)
            return false;

        PrepareRegionCoreAnalysis(regionTiles, scratch);

        int minNeighbors = GetMinNeighborsForCore(animal, rules);
        Tile best = null;
        float bestDist = float.PositiveInfinity;
        Vector3 centroid = scratch.CoreCentroid;

        for (int i = 0; i < regionTiles.Count; i++)
        {
            var t = regionTiles[i];
            if (t == null || !IsCoreTile(t, scratch, minNeighbors))
                continue;
            if (IsWaterTile(store, t))
                continue;

            float dist = (t.worldPos - centroid).sqrMagnitude;
            if (best == null || dist < bestDist - 1e-6f
                || (Mathf.Abs(dist - bestDist) <= 1e-6f && CompareTilesDeterministic(t, best) < 0))
            {
                best = t;
                bestDist = dist;
            }
        }

        if (best == null)
            return false;

        primaryCore = best;
        return true;
    }

    public static bool IsWaterTile(TileRuntimeStore store, Tile tile)
    {
        if (tile == null || store == null)
            return false;
        var rt = store.Get(tile);
        return rt != null && rt.biome == TileBiome.Water;
    }

    private static int CountCoreTilesSlow(
        IReadOnlyList<Tile> regionTiles,
        int minNeighborsForCore,
        TileRuntimeStore store)
    {
        var set = new HashSet<Tile>();
        for (int i = 0; i < regionTiles.Count; i++)
        {
            var t = regionTiles[i];
            if (t != null) set.Add(t);
        }

        int coreCount = 0;
        for (int i = 0; i < regionTiles.Count; i++)
        {
            var t = regionTiles[i];
            if (t == null || IsWaterTile(store, t))
                continue;

            int inRegion = 0;
            foreach (Tile n in HabitatRegionEnumerator.GetNeighborsSafe(t))
            {
                if (n != null && set.Contains(n))
                    inRegion++;
                if (inRegion >= minNeighborsForCore)
                {
                    coreCount++;
                    break;
                }
            }
        }

        return coreCount;
    }

    private static int CompareTilesDeterministic(Tile a, Tile b)
    {
        if (a == null || b == null) return 0;
        int c = a.q.CompareTo(b.q);
        return c != 0 ? c : a.r.CompareTo(b.r);
    }

    private static int ComputeRegionHash(IReadOnlyList<Tile> regionTiles)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < regionTiles.Count; i++)
            {
                Tile t = regionTiles[i];
                if (t != null) h = h * 31 + (t.q * 73856093 ^ t.r * 19349663);
            }
            return h;
        }
    }
}
