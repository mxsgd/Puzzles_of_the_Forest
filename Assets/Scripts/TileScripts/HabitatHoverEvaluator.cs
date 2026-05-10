using System;
using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Reusable buffers for hover preview evaluation.
/// All fields are pre-allocated — zero per-frame GC when passed across calls.
/// </summary>
public sealed class HabitatHoverScratch
{
    public readonly HashSet<Tile>         Ball           = new();
    public readonly HashSet<Tile>         Allowed        = new();
    public readonly HashSet<int>          RegionHashes   = new();  // int hash replaces string-based RegionKeys
    public readonly HashSet<Tile>         RegionSet      = new();
    public readonly List<Tile>            RegionList     = new();
    public readonly List<Tile>            NeighborScratch= new();
    public readonly List<HabitatAnimal>   YellowAnimals  = new();
    public readonly HashSet<HabitatAnimal>YellowSet      = new();  // avoids per-Evaluate HashSet alloc
    public readonly List<Tile>            Seeds          = new();  // avoids per-Evaluate List alloc

    // Per-recursion-depth frontier buffers — depth ≤ maxSize (typically 5).
    // Pre-allocated once, reused across all recursive calls; no List allocs during region enumeration.
    public readonly List<List<Tile>> FrontierStack = new()
    {
        new(), new(), new(), new(), new(), new(), new(), new()
    };
}

public enum HabitatHoverPreviewKind { Gray, Yellow, Green }

public readonly struct HabitatHoverResult
{
    public readonly HabitatHoverPreviewKind  Kind;
    public readonly HabitatAnimal            GreenAnimal;
    public readonly IReadOnlyList<HabitatAnimal> YellowAnimals;

    public HabitatHoverResult(HabitatHoverPreviewKind kind, HabitatAnimal greenAnimal, IReadOnlyList<HabitatAnimal> yellowAnimals)
    {
        Kind          = kind;
        GreenAnimal   = greenAnimal;
        YellowAnimals = yellowAnimals ?? Array.Empty<HabitatAnimal>();
    }
}

/// <summary>
/// Podgląd habitatu nad polem.
/// Wszystkie allokacje wewnętrzne są wyeliminowane przez reuse bufforów z HabitatHoverScratch.
/// </summary>
public static class HabitatHoverEvaluator
{
    // Cached delegate — avoids per-call delegate allocation from static method reference.
    private static readonly Comparison<Tile> TileComparer = CompareTilesDeterministic;

    public static void Evaluate(
        TileRuntimeStore store,
        Tile hoverTile,
        TileDraw nextDraw,
        int maxTilesPerHabitat,
        int maxGraphSteps,
        HabitatHoverScratch s,
        out HabitatHoverResult result)
    {
        result = new HabitatHoverResult(HabitatHoverPreviewKind.Gray, HabitatAnimal.None, Array.Empty<HabitatAnimal>());
        if (store == null || hoverTile == null) return;

        var hoverRt = store.Get(hoverTile);
        if (hoverRt == null) return;

        if (hoverRt.occupied)
        {
            if (hoverRt.habitatIds != null)
            {
                for (int i = hoverRt.habitatIds.Count - 1; i >= 0; i--)
                {
                    if (store.TryGetHabitatAnimal(hoverRt.habitatIds[i], out var registered) && registered != HabitatAnimal.None)
                    {
                        result = new HabitatHoverResult(HabitatHoverPreviewKind.Green, registered, Array.Empty<HabitatAnimal>());
                        return;
                    }
                }
            }
            return;
        }

        if (nextDraw == null || nextDraw.biome == TileBiome.None) return;
        if (!hoverRt.CanAcceptNewHabitat()) return;

        CollectPreviewBall(store, hoverTile, maxGraphSteps, s.Ball);
        if (s.Ball.Count == 0) return;

        s.Allowed.Clear();
        foreach (var t in s.Ball)
        {
            if (t == null) continue;
            var r = store.Get(t);
            if (t == hoverTile) { if (r.CanAcceptNewHabitat()) s.Allowed.Add(t); continue; }
            if (r.occupied && r.CanAcceptNewHabitat()) s.Allowed.Add(t);
        }

        if (!s.Allowed.Contains(hoverTile)) return;

        s.RegionHashes.Clear();
        s.YellowSet.Clear();

        // Reuse s.Seeds instead of allocating new List<Tile>.
        s.Seeds.Clear();
        foreach (var t in s.Allowed) s.Seeds.Add(t);
        s.Seeds.Sort(TileComparer);

        HabitatAnimal bestGreenAnimal  = HabitatAnimal.None;
        List<Tile>    bestGreenRegion  = null;
        float         bestGreenScore   = float.NegativeInfinity;
        int           bestGreenTileCount = int.MaxValue;

        foreach (var seed in s.Seeds)
        {
            EnumerateConnectedRegionsFromSeed(seed, s.Allowed, maxTilesPerHabitat, s,
                region =>
                {
                    if (!region.Contains(hoverTile)) return;

                    foreach (var animal in HabitatRequirements.ClassifiableAnimals)
                    {
                        if (!TryBuildSimulatedBiomeVector(store, region, animal, hoverTile, nextDraw, out var vec))
                            continue;

                        var req = HabitatRequirements.GetRequirement(animal);
                        if (vec.Satisfies(req))
                        {
                            float score   = HabitatRequirements.ComputeScore(animal, region.Count);
                            int   basePts = HabitatRequirements.GetBasePoints(animal);
                            if (IsBetterCandidate(score, basePts, region.Count, animal, region,
                                    bestGreenScore, bestGreenAnimal, bestGreenTileCount, bestGreenRegion))
                            {
                                bestGreenScore     = score;
                                bestGreenAnimal    = animal;
                                bestGreenTileCount = region.Count;
                                if (bestGreenRegion == null) bestGreenRegion = new List<Tile>();
                                else bestGreenRegion.Clear();
                                bestGreenRegion.AddRange(region); // snapshot — region is s.RegionList, safe to copy
                            }
                        }
                        else
                        {
                            if (vec.DeficitSumToward(req) == 1)
                                s.YellowSet.Add(animal);
                        }
                    }
                });
        }

        if (bestGreenAnimal != HabitatAnimal.None)
        {
            result = new HabitatHoverResult(HabitatHoverPreviewKind.Green, bestGreenAnimal, Array.Empty<HabitatAnimal>());
            return;
        }

        if (s.YellowSet.Count > 0)
        {
            s.YellowAnimals.Clear();
            foreach (var a in s.YellowSet) s.YellowAnimals.Add(a);
            s.YellowAnimals.Sort((a, b) => a.CompareTo(b));
            result = new HabitatHoverResult(HabitatHoverPreviewKind.Yellow, HabitatAnimal.None, s.YellowAnimals.ToArray());
            return;
        }

        result = new HabitatHoverResult(HabitatHoverPreviewKind.Gray, HabitatAnimal.None, Array.Empty<HabitatAnimal>());
    }

    // -------------------------------------------------------------------------
    // BFS / region enumeration
    // -------------------------------------------------------------------------

    private static void CollectPreviewBall(TileRuntimeStore store, Tile center, int maxSteps, HashSet<Tile> into)
    {
        into.Clear();
        var queue = new Queue<(Tile t, int dist)>();
        queue.Enqueue((center, 0));
        into.Add(center);
        while (queue.Count > 0)
        {
            var (t, dist) = queue.Dequeue();
            if (dist >= maxSteps) continue;
            foreach (var n in GetNeighborsSafe(t))
            {
                if (n == null || into.Contains(n)) continue;
                if (!store.Get(n).occupied) continue;
                into.Add(n);
                queue.Enqueue((n, dist + 1));
            }
        }
    }

    private static IEnumerable<Tile> GetNeighborsSafe(Tile t)
    {
        try   { return t.GetNeighbors() ?? Array.Empty<Tile>(); }
        catch { return Array.Empty<Tile>(); }
    }

    private static void EnumerateConnectedRegionsFromSeed(
        Tile seed, HashSet<Tile> allowed, int maxSize,
        HabitatHoverScratch s, Action<List<Tile>> onRegion)
    {
        s.RegionSet.Clear();
        s.RegionSet.Add(seed);
        ExtendConnectedRegion(allowed, maxSize, s, onRegion, depth: 0);
    }

    private static void ExtendConnectedRegion(
        HashSet<Tile> allowed, int maxSize,
        HabitatHoverScratch s, Action<List<Tile>> onRegion, int depth)
    {
        // Emit current region without allocating a new List — callback receives s.RegionList
        // and is synchronous, so the list is stable for the duration of the call.
        EmitCurrentRegion(s, onRegion);

        if (s.RegionSet.Count >= maxSize) return;

        CollectNeighborsOutsideRegion(allowed, s);

        // Use pre-allocated frontier buffer at this recursion depth — zero runtime alloc.
        while (s.FrontierStack.Count <= depth) s.FrontierStack.Add(new List<Tile>());
        var frontier = s.FrontierStack[depth];
        frontier.Clear();
        frontier.AddRange(s.NeighborScratch);

        foreach (var n in frontier)
        {
            s.RegionSet.Add(n);
            ExtendConnectedRegion(allowed, maxSize, s, onRegion, depth + 1);
            s.RegionSet.Remove(n);
        }
    }

    private static void EmitCurrentRegion(HabitatHoverScratch s, Action<List<Tile>> onRegion)
    {
        s.RegionList.Clear();
        foreach (var t in s.RegionSet) s.RegionList.Add(t);
        s.RegionList.Sort(TileComparer);

        // Int hash instead of string join — eliminates string allocation per region.
        int hash = ComputeRegionHash(s.RegionList);
        if (!s.RegionHashes.Add(hash)) return;

        // Pass s.RegionList directly — no new List<Tile> allocation.
        // Caller must NOT store the reference; it's a shared buffer reused per recursion level.
        onRegion?.Invoke(s.RegionList);
    }

    private static int ComputeRegionHash(List<Tile> sortedTiles)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < sortedTiles.Count; i++)
            {
                var t = sortedTiles[i];
                if (t != null) h = h * 31 + (t.q * 73856093 ^ t.r * 19349663);
            }
            return h;
        }
    }

    private static void CollectNeighborsOutsideRegion(HashSet<Tile> allowed, HabitatHoverScratch s)
    {
        s.NeighborScratch.Clear();
        foreach (var t in s.RegionSet)
        {
            foreach (var n in GetNeighborsSafe(t))
            {
                if (n == null || !allowed.Contains(n) || s.RegionSet.Contains(n)) continue;
                if (!s.NeighborScratch.Contains(n)) s.NeighborScratch.Add(n);
            }
        }
        s.NeighborScratch.Sort(TileComparer);
    }

    // -------------------------------------------------------------------------
    // Biome vector simulation
    // -------------------------------------------------------------------------

    private static bool TryBuildSimulatedBiomeVector(
        TileRuntimeStore store, List<Tile> region,
        HabitatAnimal newAnimal, Tile hoverTile, TileDraw draw, out BiomeVector vector)
    {
        vector = BiomeVector.Zero;
        foreach (var t in region)
        {
            if (t == null) continue;
            var rt = store.Get(t);
            bool isHover = ReferenceEquals(t, hoverTile);
            if (!isHover)
            {
                if (!rt.occupied || !rt.CanAcceptNewHabitat()) return false;
                if (!HabitatCompatibilityService.IsCompatibleWithAllOnTile(newAnimal, store, t)) continue;
                vector.Add(BiomeVector.FromTileBiome(rt.biome));
            }
            else
            {
                if (!rt.CanAcceptNewHabitat()) return false;
                if (!HabitatCompatibilityService.IsCompatibleWithAllOnTile(newAnimal, store, t)) continue;
                vector.Add(BiomeVector.FromTileBiome(draw.biome));
            }
        }
        return true;
    }

    // -------------------------------------------------------------------------
    // Comparison helpers
    // -------------------------------------------------------------------------

    private static bool IsBetterCandidate(
        float score, int basePoints, int tileCount, HabitatAnimal animal, List<Tile> region,
        float bestScore, HabitatAnimal bestAnimal, int bestTileCount, List<Tile> bestRegion)
    {
        const float eps = 1e-5f;
        if (score > bestScore + eps) return true;
        if (score < bestScore - eps) return false;
        int bp = HabitatRequirements.GetBasePoints(bestAnimal);
        if (basePoints > bp) return true;
        if (basePoints < bp) return false;
        if (tileCount < bestTileCount) return true;
        if (tileCount > bestTileCount) return false;
        int cmp = CompareTileLists(region, bestRegion);
        if (cmp != 0) return cmp < 0;
        return animal < bestAnimal;
    }

    private static int CompareTilesDeterministic(Tile a, Tile b)
    {
        if (a == null || b == null) return 0;
        int c = a.q.CompareTo(b.q);
        return c != 0 ? c : a.r.CompareTo(b.r);
    }

    // Both lists come pre-sorted from EmitCurrentRegion — no copy/sort needed.
    private static int CompareTileLists(List<Tile> a, List<Tile> b)
    {
        int aCount = a?.Count ?? 0;
        int bCount = b?.Count ?? 0;
        int n = Mathf.Min(aCount, bCount);
        for (int i = 0; i < n; i++)
        {
            int c = CompareTilesDeterministic(a[i], b[i]);
            if (c != 0) return c;
        }
        return aCount.CompareTo(bCount);
    }
}
